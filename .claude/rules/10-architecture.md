---
paths:
  - "app/src/**"
  - "app/tests/ArchitectureTests/**"
---
# 10 · Arquitetura (Clean Architecture + vertical slices)

Decisão: ADR-0002. Fitness functions: `app/tests/ArchitectureTests/Layers/LayerTests.cs` e `Conventions/ConventionTests.cs` — elas são a definição formal das fronteiras.

## Dependências (apontam para dentro)

```text
Web.Api ─► Infrastructure ─► Application ─► Domain ─► SharedKernel
```

- **SharedKernel**: sem dependências. **Domain**: só SharedKernel — sem EF, sem tipos da Application.
- **Application**: Domain + SharedKernel. Dados **só** via `IApplicationDbContext`; nada de `using Infrastructure.*`.
- **Infrastructure**: implementa abstrações da Application. Detalhes de persistência ficam em `IEntityTypeConfiguration<>`, nunca na entidade.
- **Web.Api**: composição + endpoints finos. Nenhuma regra de negócio.

## Application (slices)

- Uma pasta por caso de uso: `app/src/Application/{Feature}/{UseCase}/` com `{Verb}{Entity}Command`/`Get{X}Query`, `...Handler`, `...Validator`, `{X}Response`. Modelo: `Todos/Complete/`.
- Handlers `internal sealed`, primary constructor, implementam `ICommandHandler<>`/`IQueryHandler<,>`. Registrados por Scrutor — **sem MediatR, sem registro manual**.
- Validação e logging são transversais (`Abstractions/Behaviors/ValidationDecorator.cs`, `LoggingDecorator.cs`). Não repita isso nos handlers.
- Handler aplica regra de negócio (existência, propriedade, estado); não revalida o formato da entrada.
- Mutação levanta evento de domínio (`entity.Raise(new XDomainEvent(id))`) antes de `SaveChangesAsync`.
- Leituras projetam com `.Select(...)` para DTO do slice; listas retornam `PagedList<T>` (busca + ordenação + página).
- Tempo via `IDateTimeProvider`; usuário via `IUserContext`. Propriedade do recurso (padrão e legado do template que não deve ser copiado): `60-security.md`.
- Validator `internal sealed` para todo command e toda query com entrada além do id da rota — o `ValidationDecorator` envolve os dois. Paginação sempre limitada (modelo: `Todos/Get/GetTodosQueryValidator.cs`). `CreateTodoCommandValidator` e `CopyTodoCommandValidator` (`public class`) são legado.

## Cache

- HybridCache com chaves centralizadas em `{Feature}CacheKeys` (ex.: `Todos/TodoCacheKeys.cs`).
- **Invalidação simétrica**: toda leitura cacheada tem `RemoveAsync` em **toda** mutação que afeta aqueles dados.

## Web.Api

- Endpoint espelha o slice: `app/src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`, implementa `IEndpoint`, `.WithTags(Tags.X)`.
- Tradução de resultado: `result.Match(Results.Ok, CustomResults.Problem)` — nunca monte erro à mão.
- Contrato OpenAPI pelo código: `.WithSummary(...)`, `.Produces<T>()`, `.ProducesProblem(status)` por erro do handler. 401/403, 429 e idempotência são adicionados pelos transformers em `Web.Api/Extensions` (guardados por `IntegrationTests/Api/OpenApiDocumentTests.cs`) — não repita.
- Deduplicação opt-in com `.WithIdempotency()` (header `Idempotency-Key`).
