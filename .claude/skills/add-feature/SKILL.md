---
name: add-feature
description: Scaffold a complete Clean Architecture feature slice — command or query, custom handler, FluentValidation validator, minimal API endpoint, and tests (unit, validator, integration). Use when the user asks to add a feature, use case, command, query, or endpoint to this Clean Architecture template.
argument-hint: <feature description, e.g. "archive a todo item" or "get todos due this week">
---

# Add a Feature (Vertical Slice)

Scaffold a full use case following this template's conventions: an Application-layer use case with a custom command/query handler, a Web.Api minimal-API endpoint, and tests. No MediatR — this codebase uses its own `ICommand`/`IQuery` abstractions with Scrutor-registered handlers and decorators.

## Workflow

0. **Read the spec, if any.** If the request cites `SPEC-NNNN` (or a spec in `docs/02-Specs/` covers it), read it first: rules (`RN-n`) drive the handler, acceptance criteria (`AC-n`) drive the tests — tag each test with `// SPEC-NNNN AC-n`. Use the domain terms from `docs/01-Product/Glossary.md`. A spec still in `Draft`/`Review` is not ready to implement: say so.
1. **Classify the use case.** A state change is a **command**; a read is a **query**. Derive names from the existing pattern: use case verb + entity, e.g. `ArchiveTodoCommand`, `GetOverdueTodosQuery`.
2. **Check the Domain layer.** If the entity, its `{Entity}Errors` class, or a needed domain event doesn't exist, add it first (see the `add-entity` skill). Commands that change state should raise a domain event via `entity.Raise(...)`.
3. **Create the Application slice** in `app/src/Application/{Feature}/{UseCase}/` — command/query, handler, validator (commands only), response DTO (queries only). Templates: [references/command-slice.md](references/command-slice.md) and [references/query-slice.md](references/query-slice.md).
4. **Create the endpoint** in `app/src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`. Template: [references/endpoint.md](references/endpoint.md).
5. **Write tests** — handler unit tests, validator tests, and an integration test. Templates: [references/tests.md](references/tests.md).
6. **Verify:** `dotnet build CleanArchitecture.slnx`, then `dotnet test app/tests/Application.UnitTests` and `dotnet test app/tests/ArchitectureTests` (layer dependency rules) — both must pass. Run `dotnet test app/tests/IntegrationTests` too when Docker is available; without Docker, say in the report that integration tests were not run instead of claiming success.

## Non-negotiable conventions

- **Folder = use case.** One folder per use case under `app/src/Application/{Feature}/` (e.g. `Todos/Archive/`), containing all files for that slice.
- **Handlers are `internal sealed`** with primary constructors, implementing `ICommandHandler<TCommand>`, `ICommandHandler<TCommand, TResponse>`, or `IQueryHandler<TQuery, TResponse>`.
- **No manual DI registration.** Handlers, validators, and endpoints are discovered by assembly scanning (`Scrutor`, `AddValidatorsFromAssembly`, `AddEndpoints`). Never touch `DependencyInjection.cs` for a new slice.
- **Return `Result` / `Result<T>`, never throw** for expected failures. Errors come from static factory methods on `{Entity}Errors` in the Domain layer with codes like `"Todos.NotFound"`.
- **Validation lives in a `{Command}Validator`** (FluentValidation). It runs automatically via `ValidationDecorator` — the handler never validates input shape itself. Queries have no validators (the decorator only wraps commands).
- **Data access via `IApplicationDbContext`** (from `Application.Abstractions.Data`) — never reference Infrastructure from Application.
- **Ownership** in handlers that act on user-owned data: take the identity from `IUserContext.UserId` (never a `UserId` in the request), filter by it, and return `{Entity}Errors.NotFound` (404) when the resource belongs to someone else — model: `Todos/GetById/`. `Todos/Create` and `Todos/Copy` carry `UserId` in the request and answer 403 `UserErrors.Unauthorized()`: template legacy, don't copy it (`.claude/rules/60-security.md`).
- **Queries project directly to a `{X}Response` DTO** with `.Select(...)` — never return domain entities. Cache hot reads with `HybridCache` using a `{Feature}CacheKeys` static class; invalidate in the commands that mutate the cached data.
- **Endpoints** implement `IEndpoint`, resolve the handler interface directly from DI, use `result.Match(Results.Ok, CustomResults.Problem)` (or `Results.NoContent` for void commands), tag with the `Tags` class, and call `.RequireAuthorization()`.

## Naming reference

| Artifact | Pattern | Example |
|---|---|---|
| Command | `{Verb}{Entity}Command` | `ArchiveTodoCommand` |
| Query | `Get{X}Query` | `GetOverdueTodosQuery` |
| Handler | `{Command/Query}Handler` | `ArchiveTodoCommandHandler` |
| Validator | `{Command}Validator` | `ArchiveTodoCommandValidator` |
| Response | `{X}Response` | `TodoResponse` |
| Endpoint | `{UseCase}.cs` in `Endpoints/{Feature}/` | `Endpoints/Todos/Archive.cs` |
| Unit test | `{Handler}Tests` | `ArchiveTodoCommandHandlerTests` |
| Test method | `Handle_Should_{Outcome}_When{Condition}` | `Handle_Should_ReturnNotFound_WhenTodoDoesNotExist` |
