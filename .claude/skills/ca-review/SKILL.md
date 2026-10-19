---
name: ca-review
description: Quick inline checklist of pending changes against the Clean Architecture conventions — layer boundaries, Result-based error handling, slice structure, validation, endpoints, and test coverage. Use when the user asks to check conventions on a file or feature while working. For the full pre-PR review (architecture + security subagents), use the /review command instead.
argument-hint: '[optional: specific files or feature to review; defaults to the working-tree diff]'
---

# Clean Architecture Convention Review

Review the given scope (default: `git diff` + untracked files) against this template's conventions. Report findings with file:line references, ordered by severity. Do not fix anything unless asked.

## Checklist

### Layer boundaries (violations are blockers)
- Domain references only `SharedKernel` — no EF Core, no Application/Infrastructure/Web.Api types.
- Application references Domain + SharedKernel only; data access exclusively through `IApplicationDbContext`; no `using Infrastructure.*` anywhere in Application.
- Persistence details (keys, conversions, relationships) live in Infrastructure `IEntityTypeConfiguration<>` classes, not on entities.
- Web.Api endpoints contain no business logic — only request→command mapping and result matching.

### Slice structure
- One folder per use case under `app/src/Application/{Feature}/{UseCase}/`; the endpoint mirrors it at `app/src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`.
- Naming: `{Verb}{Entity}Command` / `Get{X}Query` / `...Handler` / `...Validator` / `{X}Response`.
- Handlers are `internal sealed` with primary constructors, implementing the custom `ICommandHandler<>`/`IQueryHandler<>` — no MediatR, no manual DI registration for handlers/validators/endpoints.
- Response DTOs are per-slice; queries project with `.Select(...)` and never return domain entities.

### Error handling
- Expected failures return `Result`/`Result<T>` — no exceptions for control flow, no try/catch around business rules.
- Errors come from static `{Entity}Errors` factories with `"{EntityPlural}.{Reason}"` codes and the semantically correct type (`NotFound`/`Conflict`/`Problem`/`Unauthorized`/`Forbidden`/`Failure`).
- Endpoints translate failures only via `result.Match(Results.Ok|Created|NoContent, CustomResults.Problem)`; a POST that creates answers `201 Created` with `.WithIdempotency()`.

### Validation & security
- Every command, and every query with input beyond a route id, has an `internal sealed` FluentValidation validator with length limits and bounded paging; handlers don't re-check input shape (but do enforce business rules).
- Handlers acting on user-owned data take the identity from `IUserContext.UserId` (never from the request) and filter by it; another user's resource returns `{Entity}Errors.NotFound` (404). `UserId` in the request + 403 `UserErrors.Unauthorized()` (as in `Todos/Create`, `Todos/Copy`, `Todos/Get`, `Users/GetById`) is template legacy — flag it in new code.
- Lists return `PagedList<T>`, never `List<T>`.
- New endpoints call `.RequireAuthorization()` (or `.HasPermission(...)`) and `.WithTags(Tags.X)`.
- New endpoints document their contract for OpenAPI/Scalar: `.WithSummary(...)`, `.Produces<T>(status)` for the success body (or `.Produces(204)`), and `.ProducesProblem(status)` for each status the handler's errors map to. 401/403 (auth), 429 (rate limiting) and the idempotency header/409 are added automatically — don't repeat them.
- No `DateTime.UtcNow`/`DateTime.Now` in Application — use `IDateTimeProvider`.

### State changes & caching
- Commands that mutate state raise a domain event via `entity.Raise(new XDomainEvent(id))` before `SaveChangesAsync`.
- Any `HybridCache`-cached read has matching invalidation (`cache.RemoveAsync`) in every command that mutates that data; keys come from the `{Feature}CacheKeys` class.

### Observability & personal data
- Logs use structured templates (no string interpolation) and never contain personal data (user-provided free text, names, e-mails), tokens, or connection strings.
- New metrics are named `app.<context>.<measure>` with low-cardinality tags only (no ids, e-mails, free text), and are registered with `.AddMeter(...)`/`.AddSource(...)` in `Web.Api/Extensions/OpenTelemetryExtensions.cs`.
- No new LLM/AI package or client anywhere in `app/` or `Directory.Packages.props` (ADR-0003) — blocker.

### Specs & vault
- If the change implements a spec (`docs/02-Specs/SPEC-NNNN-*`), every `AC-n` has a test tagged `// SPEC-NNNN AC-n` (`grep -rn "SPEC-NNNN" app/tests`); list the uncovered ACs.
- A new dependency, pattern, or technology has an ADR in `docs/04-ADRs/`.
- New or renamed signals are reflected in `docs/03-Architecture/Observability.md`; new domain terms in `docs/01-Product/Glossary.md`.

### Tests
- New/changed handlers have unit tests covering every `Result.Failure` path plus the happy path (persisted state + domain events).
- New/changed validators have `TestValidate` tests per rule.
- New/changed endpoints have integration tests over real HTTP.
- Test naming and Arrange/Act/Assert structure match existing tests.

## Output format

Group findings as **Blockers** (layer violations, missing auth, thrown exceptions for expected failures), **Convention violations** (naming, structure, error codes, missing events/invalidation), and **Test gaps**. For each: `file:line`, what's wrong, and the one-line fix. Close with a verdict: ready to commit, or what must change first. If everything passes, say so and run `dotnet build` + `dotnet test` to confirm.
