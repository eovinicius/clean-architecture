---
title: Visão geral da arquitetura
tags: [architecture]
---

# Visão geral da arquitetura

Monolito em **Clean Architecture** com casos de uso em vertical slices ([[ADR-0002-clean-architecture]]). Código em `app/src`, testes em `app/tests`, solução `CleanArchitecture.slnx`.

```mermaid
flowchart LR
    API["Web.Api: endpoints, OpenAPI, rate limit, idempotência"] --> INF["Infrastructure: EF Core/Npgsql, JWT, HybridCache, eventos"]
    INF --> APP["Application: slices, handlers, validators, decorators"]
    APP --> DOM["Domain: entidades, eventos, catálogos de erro"]
    DOM --> SK["SharedKernel: Result, Error, Entity"]
    APP --> SK
```

As setas são referências de projeto; as fronteiras são verificadas por `app/tests/ArchitectureTests`.

## Fluxo de uma requisição

1. Endpoint (`IEndpoint`) mapeia a requisição para um command/query.
2. `ValidationDecorator` (commands) → `LoggingDecorator` → handler.
3. Handler usa `IApplicationDbContext`, `IUserContext`, `IDateTimeProvider`; mutações levantam eventos de domínio.
4. `SaveChangesAsync` despacha eventos de domínio para `IDomainEventHandler<T>`.
5. `Result` volta ao endpoint → `result.Match(Results.Ok, CustomResults.Problem)` (`ProblemDetails` em falha).

## Infraestrutura local

`docker compose up -d`: `postgres` (17) + `observability` (Grafana LGTM). Ver [[RB-001-ambiente-local]] e [[Observability]].
