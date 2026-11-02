---
paths:
  - "app/src/**"
  - "app/tests/ArchitectureTests/**"
---
# Arquitetura (Clean Architecture + vertical slices)

Fitness functions: `app/tests/ArchitectureTests/Layers/LayerTests.cs` e `Conventions/ConventionTests.cs`. Só as regras marcadas *(build: `Teste`)* quebram o build; o resto é garantido por revisão (`ca-review`, `architecture-reviewer`) — redobre a atenção nelas.

## Dependências (apontam para dentro)

```text
Api ─► Infrastructure ─► Application ─► Domain ─► SharedKernel
```

*(build: `LayerTests`)*

- **SharedKernel**: sem dependências. **Domain**: só SharedKernel — sem EF, sem tipos da Application.
- **Application**: Domain + SharedKernel. Dados **só** via `IApplicationDbContext`; nada de `using Infrastructure.*`.
- **Infrastructure**: implementa abstrações da Application. Detalhes de persistência ficam em `IEntityTypeConfiguration<>`, nunca na entidade.
- **Serviço externo novo** (e-mail, armazenamento, API de terceiro): interface em `Application/Abstractions/{Área}/I{X}.cs` (ex.: `Abstractions/Notifications/IEmailSender.cs`), implementação em `Infrastructure/{Área}/`, registro no `DependencyInjection.cs` da Infrastructure.
- **Api**: composição + endpoints finos. Nenhuma regra de negócio.

## Domain (modelo rico)

Modelo: a entidade existente mais parecida em `app/src/Domain/{Feature}/`.

- **Sem entidade anêmica.** Estado só muda por método da entidade: propriedades `{ get; private set; }` (nunca `set`/`init` público) *(build: `DomainClasses_Should_NotHavePublicSetters`)* e construtor privado sem parâmetros para o EF *(build: `Entities_Should_HavePrivateParameterlessConstructor`)*.
- Criação por factory estática `{Entity}.Create(...)` (ou verbo de negócio, ex.: `RefreshToken.Issue`): gera o `Id`, copia coleções recebidas (`[.. labels]`) e levanta `{Entity}CreatedDomainEvent`.
- Cada transição é um método com nome de negócio (`Complete`, `Archive`, `UpdateDescription`) que valida a invariante, retorna `Result` quando a regra pode falhar (`void` quando não pode) e **levanta o próprio evento** — o handler não chama `Raise`. A falha é o erro do catálogo retornado direto: `return {Entity}Errors.{Reason}(Id);` (conversão implícita em `SharedKernel/Result.cs`).
- Catálogo de erros: `public static class {Entity}Errors` ao lado da entidade (`Domain/{Feature}/{Entity}Errors.cs`), um método por motivo, código `"{EntityPlural}.{Reason}"` e o `ErrorType` que define o status HTTP (seção Api).
- Valores não determinísticos entram por parâmetro (`DateTime utcNow`); a entidade não resolve relógio nem usuário.

## Application (slices)

- Uma pasta por caso de uso: `app/src/Application/{Feature}/{UseCase}/` com `{Verb}{Entity}Command`/`Get{X}Query`, `...Handler`, `...Validator`, `{X}Response`.
- Handlers `internal sealed` *(build: `Handlers_Should_BeInternalAndSealed`)*, primary constructor, implementam `ICommandHandler<>`/`IQueryHandler<,>`. Registrados por Scrutor — **sem MediatR** *(build: `Solution_Should_NotHaveDependencyOn_MediatR`)*, **sem registro manual**.
- Validação e logging são transversais (`Abstractions/Behaviors/ValidationDecorator.cs`, `LoggingDecorator.cs`). Não repita isso nos handlers.
- Handler **orquestra**, nesta ordem:
  1. busca com filtro de propriedade (existência + dono → `return {Entity}Errors.NotFound(id);`);
  2. chama o método da entidade;
  3. propaga a falha: `return result;` se o tipo bate, `return result.Error;` se o handler devolve `Result<T>`;
  4. `SaveChangesAsync`;
  5. invalida o cache (seção Cache).

  Falha sempre como `Error` direto — nunca `Result.Failure(...)`/`Result.Failure<T>(...)`. Regra de estado fica na entidade (Domain, acima); o handler não revalida o formato da entrada nem atribui propriedades.
- Lógica repetida entre handlers vai para um helper `internal static` na raiz da feature (ex.: `Users/RefreshTokenIssuer.cs`), não é copiada. DTO usado por mais de um slice também fica na raiz da feature (ex.: `Users/AccessTokensResponse.cs`).
- Leituras projetam com `.Select(...)` para DTO do slice; listas retornam `PagedList<T>` (busca + ordenação + página).
- Tempo via `IDateTimeProvider`; usuário via `IUserContext`. Propriedade do recurso (padrão e legado do template que não deve ser copiado): `security.md`.
- Validator `internal sealed` *(build: `Validators_Should_BeInternalAndSealed`)* para todo command *(build: `Commands_Should_HaveValidator`)* e toda query com entrada além do id da rota — o `ValidationDecorator` envolve os dois. Paginação sempre limitada. Exceções: command disparado só internamente, sem entrada externa (ex.: `DeleteExpiredUnconfirmedUsersCommand`, do background job), dispensa validator e entra em `InternalCommands` no teste; os validators `public class` da allowlist de legado em `ConventionTests` não são replicados.

## Cache

- HybridCache com chaves centralizadas em `{Feature}CacheKeys` (`Application/{Feature}/{Feature}CacheKeys.cs`).
- **Invalidação simétrica**: toda leitura cacheada tem `RemoveAsync` em **toda** mutação que afeta aqueles dados.

## Api

- Endpoint espelha o slice: `app/src/Api/Endpoints/{Feature}/{UseCase}.cs`, implementa `IEndpoint` *(build: `Endpoints_Should_BeInternalAndSealed`)*, `.WithTags(Tags.X)`.
- Tradução de resultado: `result.Match(Results.Ok, CustomResults.Problem)` — nunca monte erro à mão. `Api/Infrastructure/CustomResults.cs` mapeia `ErrorType` → status: `Validation`/`Problem` 400, `Unauthorized` 401, `Forbidden` 403, `NotFound` 404, `Conflict` 409.
- Contrato OpenAPI pelo código: `.WithSummary(...)`, `.Produces<T>()`, `.ProducesProblem(status)` por erro do handler (status pelo mapeamento acima). 401/403, 429 e idempotência são adicionados pelos transformers em `Api/Extensions` (guardados por `IntegrationTests/Api/OpenApiDocumentTests.cs`) — não repita.
- Deduplicação opt-in com `.WithIdempotency()` (header `Idempotency-Key`).
- Composição em `Api/Extensions`: uma classe de extensão por preocupação (ex.: `CorsExtensions.cs`, `RateLimitingExtensions.cs`), não um arquivo genérico que acumula registros.
- Background jobs em `Api/BackgroundJobs/` (`BackgroundService`) só agendam e disparam um command — a regra fica no handler (ex.: `UnconfirmedUsersCleanupService.cs` → `DeleteExpiredUnconfirmedUsersCommand`). Skill `add-background-job`.
