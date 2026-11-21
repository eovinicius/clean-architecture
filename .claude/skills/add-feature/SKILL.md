---
name: add-feature
description: Scaffold a complete Clean Architecture feature slice — command or query, custom handler, FluentValidation validator, controller action, and tests (unit, validator, integration). Use when the user asks to add a feature, use case, command, query, or endpoint to this Clean Architecture template.
argument-hint: <feature description, e.g. "archive a {entity}" or "list {entities} due this week">
---

# Add a Feature (Vertical Slice)

Scaffold a full use case following this template's conventions: an Application-layer use case with a custom command/query handler, an action on the feature's Api controller, and tests. No MediatR — this codebase uses its own `ICommand`/`IQuery` abstractions with Scrutor-registered handlers and decorators.

## Workflow

0. **Spec gate.** If the request cites `SPEC-NNNN` (or a spec in `docs/02-Specs/` covers it), read it first: rules (`RN-n`) drive the handler, acceptance criteria (`AC-n`) drive the tests — one test (at least) per AC. Use the domain terms from `docs/01-Product/Glossary.md`. A spec still in `Draft`/`Review` is not ready to implement: say so. **No `Approved` spec** and the request is a new feature, a business-rule change, a new endpoint or an HTTP contract change (`.claude/rules/specs.md`)? Stop and propose the `write-spec` skill instead of scaffolding — a bugfix or internal adjustment goes straight on.
1. **Classify the use case.** A state change is a **command**; a read is a **query**. Derive names from the existing pattern: use case verb + entity, e.g. `{Verb}{Entity}Command`, `Get{X}Query`.
2. **Check the Domain layer.** If the entity, its `{Entity}Errors` class, or a needed domain event doesn't exist, add it first (see the `add-entity` skill). A command that changes state needs a **behavior method on the entity** (validates the rule, returns `Result` and, when the feature uses domain events, raises its own) — add it to the entity with an `{Entity}Tests` test; the handler never assigns properties or calls `Raise`. Rich domain rules: `.claude/rules/architecture.md` (Domain).
3. **Create the Application slice** in `app/src/Application/{Feature}/{UseCase}/` — command/query, handler, validator (every command, and every query with input beyond a route id), response DTO (queries only). Templates: [references/command-slice.md](references/command-slice.md) and [references/query-slice.md](references/query-slice.md).
4. **Add the controller action** to `app/src/Api/Controllers/{Feature}Controller.cs` (create the controller for a new feature), with its request body in `app/src/Api/Controllers/{Feature}/`. Template: [references/controller.md](references/controller.md).
5. **Write tests** — handler unit tests, validator tests, and an integration test. Templates: [references/tests.md](references/tests.md).
6. **Verify:** `dotnet build CleanArchitecture.slnx`, then `dotnet test app/tests/Application.UnitTests` and `dotnet test app/tests/ArchitectureTests` (layer dependency rules) — both must pass. Run `dotnet test app/tests/IntegrationTests` too when Docker is available; without Docker, say in the report that integration tests were not run instead of claiming success.

## Non-negotiable conventions

- **Folder = use case.** One folder per use case under `app/src/Application/{Feature}/` (`{Feature}/{UseCase}/`), containing all files for that slice.
- **Handlers are `internal sealed`** with primary constructors, implementing `ICommandHandler<TCommand>`, `ICommandHandler<TCommand, TResponse>`, or `IQueryHandler<TQuery, TResponse>`. They orchestrate only: load through the repository (ownership-filtered) → call the entity method → propagate failure → save through `IUnitOfWork` → invalidate cache.
- **Every concrete class/record is `sealed`** (commands, DTOs, requests, tests) — `.claude/rules/csharp.md`.
- **No manual DI registration.** Handlers, validators, and controllers are discovered automatically (`Scrutor`, `AddValidatorsFromAssembly`, `MapControllers`). Only a new repository is registered by hand, in the Infrastructure `DependencyInjection.cs`.
- **Return `Result` / `Result<T>`, never throw** for expected failures. Errors come from static factory methods on `{Entity}Errors` in the Domain layer, with codes prefixed by the entity plural already used in that catalog (`"{EntityPlural}.NotFound"`, `"Users.NotFound"`).
- **Validation lives in an `internal sealed` `{Command}Validator` / `{Query}Validator`** (FluentValidation), run automatically by `ValidationDecorator` — the handler never validates input shape itself. Which slices need one, paging bounds and the model: `.claude/rules/architecture.md` (Application).
- **Data access via `I{Entity}Repository` + `IUnitOfWork`** (from `Application.Abstractions.Data`) — no EF Core and no Infrastructure reference in the Application. Model: `.claude/rules/architecture.md` (Dados e respostas).
- **Ownership** in handlers that act on user-owned data: identity from `IUserContext.UserId` (never a `UserId` in the request), 404 for another user's resource. Model, and the template legacy not to copy: `.claude/rules/security.md`.
- **Queries map the entity to a `{X}Response` DTO** (`{X}Response.From(entity)`, `PagedList<T>.Map`) — never return domain entities. Each slice owns its DTO, even when another slice has an identical one. Lists return `PagedList<T>` paged by the repository. Cache hot single-item reads with `HybridCache` using a `{Feature}CacheKeys` static class; invalidate in **every** command that mutates the cached data.
- **Controller actions** (one per use case, `Task<IResult>`) inject the handler with `[FromServices]`, translate with `result.Match(..., CustomResults.Problem)` — `Results.Created` (POST that creates, plus `[Idempotent]`), `Results.NoContent` (void command) or `Results.Ok` — and document the contract with `[EndpointSummary]` + `[ProducesResponseType...]`. The controller carries `[Tags(Tags.{Feature})]` and `[Authorize]`.

## Naming reference

| Artifact | Pattern |
| --- | --- |
| Command | `{Verb}{Entity}Command` |
| Query | `Get{X}Query` |
| Handler | `{Command/Query}Handler` |
| Validator | `{Command/Query}Validator` |
| Response | `{X}Response` |
| Controller | `{Feature}Controller.cs` in `Api/Controllers/` (one action per use case) |
| Request body | `{UseCase}Request.cs` in `Api/Controllers/{Feature}/` |
| Unit test | `{Handler}Tests` |
| Test method | `Handle_Should_{Outcome}_When{Condition}` |
