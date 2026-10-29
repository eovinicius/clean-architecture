---
paths:
  - "app/src/**"
  - "app/tests/ArchitectureTests/**"
---
# Arquitetura (Clean Architecture + vertical slices)

Decisão: ADR-0002. Fitness functions: `app/tests/ArchitectureTests/Layers/LayerTests.cs` e `Conventions/ConventionTests.cs` — elas são a definição formal das fronteiras.

## Dependências (apontam para dentro)

```text
Web.Api ─► Infrastructure ─► Application ─► Domain ─► SharedKernel
```

- **SharedKernel**: sem dependências. **Domain**: só SharedKernel — sem EF, sem tipos da Application.
- **Application**: Domain + SharedKernel. Dados **só** via `IApplicationDbContext`; nada de `using Infrastructure.*`.
- **Infrastructure**: implementa abstrações da Application. Detalhes de persistência ficam em `IEntityTypeConfiguration<>`, nunca na entidade.
- **Web.Api**: composição + endpoints finos. Nenhuma regra de negócio.

## Domain (modelo rico — ADR-0004)

Modelo: `app/src/Domain/Todos/TodoItem.cs`. Fitness: `DomainClasses_Should_NotHavePublicSetters` em `ConventionTests`.

- **Sem entidade anêmica.** Estado só muda por método da entidade: propriedades `{ get; private set; }` (nunca `set`/`init` público) e construtor privado sem parâmetros para o EF.
- Criação por factory estática `{Entity}.Create(...)` (ou verbo de negócio, ex.: `RefreshToken.Issue`): gera o `Id`, copia coleções recebidas (`[.. labels]`) e levanta `{Entity}CreatedDomainEvent`.
- Cada transição é um método com nome de negócio (`Complete`, `Archive`, `UpdateDescription`) que valida a invariante, retorna `Result` quando a regra pode falhar (`void` quando não pode) e **levanta o próprio evento** — o handler não chama `Raise`. A falha é o erro do catálogo retornado direto: `return TodoItemErrors.Archived(Id);` (conversão implícita em `SharedKernel/Result.cs`).
- Valores não determinísticos entram por parâmetro (`DateTime utcNow`); a entidade não resolve relógio nem usuário.
- Comentário de regra (`// SPEC-NNNN RN-n`) fica no método da entidade que a implementa.

## Application (slices)

- Uma pasta por caso de uso: `app/src/Application/{Feature}/{UseCase}/` com `{Verb}{Entity}Command`/`Get{X}Query`, `...Handler`, `...Validator`, `{X}Response`. Modelo: `Todos/Complete/`.
- Handlers `internal sealed`, primary constructor, implementam `ICommandHandler<>`/`IQueryHandler<,>`. Registrados por Scrutor — **sem MediatR, sem registro manual**.
- Validação e logging são transversais (`Abstractions/Behaviors/ValidationDecorator.cs`, `LoggingDecorator.cs`). Não repita isso nos handlers.
- Handler **orquestra**: busca com filtro de propriedade (existência + dono → `return {Entity}Errors.NotFound(id);`), chama o método da entidade, propaga a falha (`return result;` se o tipo bate, `return result.Error;` se o handler devolve `Result<T>`), `SaveChangesAsync`, invalida cache. Falha sempre como `Error` direto — nunca `Result.Failure(...)`/`Result.Failure<T>(...)`. Regra de estado fica na entidade (Domain, acima); o handler não revalida o formato da entrada nem atribui propriedades.
- Lógica repetida entre handlers vai para um helper `internal static` da feature (ex.: `Users/RefreshTokenIssuer.cs`), não é copiada.
- Leituras projetam com `.Select(...)` para DTO do slice; listas retornam `PagedList<T>` (busca + ordenação + página).
- Tempo via `IDateTimeProvider`; usuário via `IUserContext`. Propriedade do recurso (padrão e legado do template que não deve ser copiado): `security.md`.
- Validator `internal sealed` para todo command e toda query com entrada além do id da rota — o `ValidationDecorator` envolve os dois. Paginação sempre limitada (modelo: `Todos/Get/GetTodosQueryValidator.cs`). `CreateTodoCommandValidator` e `CopyTodoCommandValidator` (`public class`) são legado.

## Cache

- HybridCache com chaves centralizadas em `{Feature}CacheKeys` (ex.: `Todos/TodoCacheKeys.cs`).
- **Invalidação simétrica**: toda leitura cacheada tem `RemoveAsync` em **toda** mutação que afeta aqueles dados.

## Web.Api

- Endpoint espelha o slice: `app/src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`, implementa `IEndpoint`, `.WithTags(Tags.X)`.
- Tradução de resultado: `result.Match(Results.Ok, CustomResults.Problem)` — nunca monte erro à mão.
- Contrato OpenAPI pelo código: `.WithSummary(...)`, `.Produces<T>()`, `.ProducesProblem(status)` por erro do handler. 401/403, 429 e idempotência são adicionados pelos transformers em `Web.Api/Extensions` (guardados por `IntegrationTests/Api/OpenApiDocumentTests.cs`) — não repita.
- Deduplicação opt-in com `.WithIdempotency()` (header `Idempotency-Key`).
- Composição em `Web.Api/Extensions`: uma classe de extensão por preocupação (ex.: `CorsExtensions.cs`, `RateLimitingExtensions.cs`), não um arquivo genérico que acumula registros.
