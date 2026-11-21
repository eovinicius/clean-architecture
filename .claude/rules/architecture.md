---
paths:
  - "app/src/**"
  - "app/tests/ArchitectureTests/**"
---

# Arquitetura (Clean Architecture + vertical slices)

## Dependências (apontam para dentro)

```text
Api ──────────────────────► Application ─► Domain ─► SharedKernel
 │                               ▲
 └────► Infrastructure ──────────┘
```

- **SharedKernel**: sem dependências.
- **Domain**: só SharedKernel — sem dependência de infraestrutura, sem tipos da Application.
- **Application**: Domain + SharedKernel. Dados via interface de repository (`I{Entity}Repository`) ou de service (`I{Servico}Service`), gravação via `IUnitOfWork`; nada de `using Infrastructure.*` nem de EF Core.
- **Infrastructure**: implementa abstrações da Application. Clientes downstream com typed `HttpClient` (`IHttpClientFactory`) e resiliência (retry, timeout, circuit breaker). Utiliza os DTOs definidos na Application, sem duplicar modelos de transporte.
- **Api**: depende de Application e Infrastructure. Composição + endpoints finos. Nenhuma regra de negócio.

## Domain (modelo rico)

Modelo: `app/src/Domain/Todos/TodoItem.cs`.

- **Sem entidade anêmica.** Estado só muda por método da entidade: propriedades `{ get; private set; }` (nunca `set`/`init` público).
- **Criação** por factory estática `{Entity}.Create(...)` (ou verbo de negócio, ex.: `RefreshToken.Issue`): gera o `Id` e copia coleções recebidas (`[.. labels]`).
- **Reidratação**: com EF Core, pelo construtor privado sem parâmetros (o EF grava pelos `private set`). Quando o estado vem de um downstream HTTP, por factory estática `{Entity}.Restore(...)`: recebe o estado completo, sem validar regra. Uso exclusivo da Infrastructure; criação nova sempre por `Create`.
- **Transições**: cada uma é um método com nome de negócio (`Complete`, `Archive`, `UpdateDescription`) que valida a invariante.
  - Retorna `Result` quando a regra pode falhar; `void` quando não pode.
  - A falha é o erro do catálogo retornado direto: `return TodoItemErrors.Archived(Id);` (conversão implícita em `SharedKernel/Result.cs`).
- **Domain events (opcional)**: só quando outro efeito precisa reagir ao fato (ex.: e-mail, auditoria).
  - `public sealed record {Entity}{Fato}DomainEvent(...) : IDomainEvent` ao lado da entidade (`Domain/{Feature}/`).
  - Levantado pela própria entidade (`Raise(...)`) dentro de `Create`/transição — nunca pelo handler; `Restore` não levanta evento.
  - Reação em `IDomainEventHandler<T>` `internal sealed` na Application, registrado por Scrutor e despachado pela Infrastructure depois de persistir (consistência eventual: a falha da reação não desfaz a transição).
- Valores não determinísticos entram por parâmetro (`DateTime utcNow`); a entidade não resolve relógio nem usuário.

## Application (slices)

### Estrutura

- Uma pasta por caso de uso: `app/src/Application/{Feature}/{UseCase}/` com `{Verb}{Entity}Command`/`Get{X}Query`, `...Handler`, `...Validator`, `{X}Response`. Modelo: `Todos/Complete/`.
- Handlers `internal sealed`, primary constructor, implementam `ICommandHandler<>`/`IQueryHandler<,>`. Registrados por Scrutor — **sem MediatR, sem registro manual**.
- Validação e logging são transversais (`Abstractions/Behaviors/ValidationDecorator.cs`, `LoggingDecorator.cs`). Não repita isso nos handlers.
- Validator `internal sealed` para todo command e toda query com entrada além do id da rota — o `ValidationDecorator` envolve os dois.
  - Paginação sempre limitada (modelo: `Todos/Get/GetTodosQueryValidator.cs`).
  - `CreateTodoCommandValidator` e `CopyTodoCommandValidator` (`public class`) são legado.

### Handler

- Handler **orquestra**, nesta ordem:
  1. Busca via `I{Entity}Repository` com filtro de propriedade (existência + dono → `{Entity}Errors.NotFound(id)`).
  2. Chama o método da entidade.
  3. Propaga a falha: `return result;` se o tipo bate, `return result.Error;` se o handler devolve `Result<T>`.
  4. Persiste: `Add`/`Remove` no `I{Entity}Repository` e **uma** chamada a `IUnitOfWork.SaveChangesAsync` (atômica entre agregados; entidade buscada já é rastreada).
- Falha sempre como `Error` direto — nunca `Result.Failure(...)`/`Result.Failure<T>(...)`.
- Regra de estado fica na entidade (Domain, acima); o handler não revalida o formato da entrada nem atribui propriedades.
- Chamadas independentes podem ser executadas em paralelo (`Task.WhenAll`) quando seguro e adequado; chamadas dependentes são sequenciais. Propagação de `CancellationToken`: `csharp.md`.
- Lógica repetida entre handlers vai para um helper `internal static` da feature (ex.: `Users/RefreshTokenIssuer.cs`), não é copiada.
- Tempo via `IDateTimeProvider`; usuário via `IUserContext`. Propriedade do recurso (padrão e legado do template que não deve ser copiado): `security.md`.

### Dados e respostas

- Os DTOs necessários às chamadas downstream ficam na Application, junto à interface do repository/service ou na pasta da feature correspondente. A Infrastructure utiliza esses mesmos tipos para serializar requisições e desserializar respostas, sem criar DTOs duplicados.
- Leituras mapeiam a entidade para o DTO do slice, nunca devolvem a entidade.
- Listas retornam `PagedList<T>` paginado pelo repository (busca + ordenação + página repassadas ao downstream); o handler converte com `PagedList<T>.Map(...)`.
- Interfaces de dados em `Application/Abstractions/Data/` (`I{Entity}Repository`, `IUnitOfWork`, filtros de lista); implementação `internal sealed` em `Infrastructure/Database/Repositories/{Entity}Repository.cs`, registrada no `DependencyInjection.cs` da Infrastructure. Conflito de concorrência chega ao handler como `ConcurrencyConflictException`, nunca como exceção do EF.

## Api

### Controller

- Controller tradicional por feature: `app/src/Api/Controllers/{Feature}Controller.cs`.
  - `public sealed`, herda `ControllerBase`.
  - `[ApiController]`, `[Route("{feature}")]`, `[Tags(Tags.{Feature})]`, `[Authorize]`.
- Uma action por caso de uso (`Task<IResult>`): só monta o command/query, chama o handler injetado com `[FromServices]` e traduz o resultado.
  - Anônima só com `[AllowAnonymous]` (ver `security.md`); permissão com `[HasPermission(Permissions.X)]`; rate limit específico com `[EnableRateLimiting(RateLimitingPolicies.X)]`.
  - Corpo da requisição: `public sealed record {UseCase}Request` em `Api/Controllers/{Feature}/` (schema OpenAPI `{Feature}{UseCase}Request`, ex.: `TodosCreateRequest`).
- Tradução de resultado: `result.Match(Results.Ok, CustomResults.Problem)` — nunca monte erro à mão.
- Deduplicação opt-in com `[Idempotent]` na action (`Api/Infrastructure/IdempotencyFilter.cs`, header `Idempotency-Key`).

### Contrato OpenAPI

- Declarado pelo código: `[EndpointSummary("...")]`, `[ProducesResponseType<T>(StatusCodes.Status200OK)]` e `[ProducesResponseType<ProblemDetails>(status, MediaTypeNames.Application.ProblemJson)]` por erro do handler.
- 401/403, 429 e idempotência são adicionados pelos transformers em `Api/Extensions` (guardados por `IntegrationTests/Api/OpenApiDocumentTests.cs`) — não repita.

### Composição

- Em `Api/Extensions`: uma classe de extensão por preocupação (ex.: `CorsExtensions.cs`, `RateLimitingExtensions.cs`), não um arquivo genérico que acumula registros.
