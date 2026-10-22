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

### Domain model (`.claude/rules/10-architecture.md`, Domain — ADR-0004)
- No anemic entities: properties `{ get; private set; }`, private parameterless constructor, static `Create(...)` factory raising the `Created` event.
- Every state change is an entity method that enforces the rule, returns `Result` (with an `{Entity}Errors` error) when it can fail and raises its own domain event. A handler assigning entity properties or calling `Raise` is a convention violation.
- Logic duplicated across handlers is extracted (entity method or feature `internal static` helper), not copied.
- Every concrete class/record is `sealed` unless it is a real inheritance base (`.claude/rules/20-csharp.md`); exceptions thrown are specific types, never `Exception`/`ApplicationException`.

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
- Validators per `.claude/rules/10-architecture.md` (Application): every command and every query with input beyond a route id, with length limits and bounded paging; handlers don't re-check input shape (but do enforce business rules).
- Ownership per `.claude/rules/60-security.md`: identity from `IUserContext.UserId` (never from the request), filtered, 404 for another user's resource. New code copying the template legacy listed there is a finding.
- Lists return `PagedList<T>`, never `List<T>`.
- New endpoints call `.RequireAuthorization()` (or `.HasPermission(...)`) and `.WithTags(Tags.X)`.
- New endpoints document their OpenAPI contract per `.claude/rules/10-architecture.md` (Web.Api): `.WithSummary(...)`, `.Produces<T>(status)` (or `.Produces(204)`) and `.ProducesProblem(status)` per handler error — and don't repeat the responses the transformers already add.
- No `DateTime.UtcNow`/`DateTime.Now` in Application — use `IDateTimeProvider`.

### State changes & caching
- Commands that mutate state do it through an entity method that raises the domain event before `SaveChangesAsync`; the handler propagates the method's failure (`if (result.IsFailure) return result;`).
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
- New/changed entity methods have `{Entity}Tests` (success, each failure, event raised or not); tests seed through factories/methods + `ClearDomainEvents()`, never object initializers or reflection.
- New/changed validators have `TestValidate` tests per rule.
- New/changed endpoints have integration tests over real HTTP.
- Test naming and Arrange/Act/Assert structure match existing tests.

## Output format

Group findings as **Blockers** (layer violations, missing auth, thrown exceptions for expected failures), **Convention violations** (naming, structure, error codes, missing events/invalidation), and **Test gaps**. For each: `file:line`, what's wrong, and the one-line fix. Close with a verdict: ready to commit, or what must change first. If everything passes, say so and run `dotnet build` + `dotnet test` to confirm.
