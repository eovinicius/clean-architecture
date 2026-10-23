---
name: ca-review
description: Quick inline review of work in progress against this repository's Clean Architecture conventions — layer boundaries, rich domain model, slice structure, Result-based errors, validation, resource ownership, endpoint contract, caching, logging of personal data, migrations, and test coverage. Use whenever the user asks to check, review, or sanity-check a file, slice, handler, endpoint, or their pending changes against the project's patterns ("does this follow the conventions?", "check my slice", "is this handler right?"), even if they never say "conventions". For hunting logic bugs use the code-reviewer subagent; for the full pre-PR review with security, use the /review command.
argument-hint: '[optional: files, folder, or feature to review; defaults to everything the branch changed]'
---

# Clean Architecture Convention Review

Report-only: list findings with `file:line` and do not edit anything unless the user asks. Work through the five steps in order — each one narrows what the next has to look at, which is what keeps the review quick.

## 1. Scope

An explicit argument wins. Otherwise review everything the branch changed, which takes three commands because each one alone misses work:

- `git diff development...HEAD` — committed on the branch (base is `main` for `hotfix/*`; skip on `development` itself);
- `git diff HEAD` — staged and unstaged;
- `git status --porcelain` — untracked files.

State the scope you reviewed in one line. If it is empty, say so and stop — don't fall back to reviewing the whole repository.

Judge only what the diff adds or changes. Pre-existing code is out of scope, including the template legacy documented in `.claude/rules/60-security.md` and `.claude/rules/10-architecture.md` (e.g. `UserId` in `Todos/Create`, `CreateTodoCommandValidator` as a `public class`): touching a line next to legacy is not a finding, but **new** code copying the legacy is.

## 2. Mechanical checks

When the scope touches `app/`, run these before reading the code:

1. `dotnet build CleanArchitecture.slnx` — analyzers run as errors, so this also catches style and nullability.
2. `dotnet test app/tests/ArchitectureTests` — `LayerTests` and `ConventionTests` are the formal definition of layer boundaries, `sealed`, public setters on entities, and `UserId` in new requests. Report each failure as a blocker naming the test; don't re-derive by hand what they already prove.
3. `dotnet test app/tests/Application.UnitTests`.

`IntegrationTests` need Docker. Run them only if `docker info` succeeds; otherwise list them under "Not verified" — they include `EndpointAuthorizationTests` and `OpenApiDocumentTests`, so endpoint authorization and the OpenAPI contract must then be checked by reading.

A failing step is a finding, not a reason to stop: report it and keep reviewing.

## 3. Triage

Map the changed files to the checklist sections and skip sections with nothing in scope:

| Changed | Sections |
|---|---|
| `app/src/Domain/` | Layers, Domain model, Tests |
| `app/src/Application/` | Layers, Slice, Errors, Validation & ownership, State & cache, C#, Observability, Tests |
| `app/src/Web.Api/Endpoints/` | Endpoint, Tests |
| `app/src/Infrastructure/` (configurations, migrations) | Layers, Persistence |
| `Directory.Packages.props`, `*.csproj` | C# (packages) |
| a spec (`SPEC-NNNN` in the branch, commits, or tests) | Specs & vault |

## 4. Checklist

Each section names the rule that owns the convention; open it when an item needs detail or you're unsure. If an item here and its rule disagree, the rule wins — follow it and report the drift.

### Layers — `.claude/rules/10-architecture.md`
- Application never uses `Infrastructure.*`; data access only through `IApplicationDbContext`.
- Keys, conversions, indexes, and relationships live in `IEntityTypeConfiguration<>`, never on the entity; FK ids, no navigation properties.
- Endpoints only map request → command and match the result; no business logic.

### Domain model — `.claude/rules/10-architecture.md` (ADR-0004)
- `{ get; private set; }`, private parameterless constructor, static `Create(...)` that raises the `Created` event.
- Every state change is a business-named entity method that enforces the invariant, returns `Result` when it can fail, and raises its own event. A handler assigning properties or calling `Raise`, or an entity method that changes state without raising an event, is a violation.
- Non-deterministic values (time) come in as parameters; exceptions are specific types, never `Exception`/`ApplicationException`.

### Slice — `.claude/rules/10-architecture.md`
- `app/src/Application/{Feature}/{UseCase}/` mirrored by `app/src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`; names `{Verb}{Entity}Command`/`Get{X}Query`/`...Handler`/`...Validator`/`{X}Response`.
- Handlers `internal sealed`, primary constructor, `ICommandHandler<>`/`IQueryHandler<,>`; no MediatR, no manual DI registration.
- Reads project with `.Select(...)` into the slice's DTO, never return entities; lists return `PagedList<T>`.
- No validation or logging the decorators already do (`Abstractions/Behaviors/ValidationDecorator.cs`, `LoggingDecorator.cs`); logic repeated across handlers goes to an entity method or feature helper.

### Errors — `AGENTS.md` §4
- Fail by returning the catalog error directly (`return TodoItemErrors.NotFound(id);`); propagate with `return result;` / `return result.Error;`. `Result.Failure(...)`/`Result.Failure<T>(...)` in a handler or entity is a violation.
- `{Entity}Errors` factories with `"{EntityPlural}.{Reason}"` codes and the right `ErrorType`.
- No exceptions or try/catch for expected failures.

### Validation & ownership — `.claude/rules/10-architecture.md`, `.claude/rules/60-security.md`
- A validator for every command and every query with input beyond the route id: length limits, `IsInEnum` for enums, bounded paging.
- Identity comes from `IUserContext.UserId`, never from the request; the query filters by it and another user's resource returns `{Entity}Errors.NotFound` (404).
- Time via `IDateTimeProvider` — no `DateTime.UtcNow`/`DateTime.Now` in Application.

### Endpoint — `.claude/rules/10-architecture.md`, `.claude/rules/60-security.md`
- `.RequireAuthorization()` or `.HasPermission(...)`; an intentionally anonymous endpoint is justified and listed in `IntegrationTests/Api/EndpointAuthorizationTests.cs`.
- `.WithTags(Tags.X)`, `.WithSummary(...)`, `.Produces<T>(status)` (or `.Produces(204)`), and `.ProducesProblem(status)` per handler error — without repeating the 401/403/429/idempotency responses the transformers add.
- `result.Match(Results.Ok|Created|NoContent, CustomResults.Problem)`; a creating POST answers `201 Created` with `.WithIdempotency()`.

### State & cache — `.claude/rules/10-architecture.md`
- Mutation through the entity method, failure propagated, then `SaveChangesAsync`.
- Every command that mutates cached data calls `cache.RemoveAsync` with a key from `{Feature}CacheKeys`.

### C# — `.claude/rules/20-csharp.md`
- Async end to end: the received `CancellationToken` reaches the last call (not `CancellationToken.None`/`default`); no `.Result`/`.Wait()`/`async void`.
- Package versions only in `Directory.Packages.props`; a new package is justified (ADR if structural); no LLM/AI package anywhere (ADR-0003).

### Observability & personal data — `.claude/rules/50-observability.md`, `.claude/rules/60-security.md`
- Structured log templates; no personal data (names, e-mails, user-provided free text such as a todo description), tokens, or connection strings in logs, traces, metric tags, or error messages.
- Metrics named `app.<context>.<measure>` with low-cardinality tags, registered with `.AddMeter(...)`/`.AddSource(...)` in `Web.Api/Extensions/OpenTelemetryExtensions.cs`.

### Persistence — `.claude/rules/70-persistence.md`
- Migrations generated by `dotnet ef` (Designer and snapshot in the same diff); merged migrations untouched.
- Drop/rename → expand/contract; `NOT NULL` on an existing table → default or backfill; new filter or sort → index considered.

### Tests — `.claude/rules/30-testing.md`
- Handler: happy path (persisted state + domain events) and every failure path.
- Entity method: `{Entity}Tests` for success, each failure, and the event raised (or not); state seeded through factory + methods + `ClearDomainEvents()`.
- Validator: `TestValidate` per rule. Endpoint: integration test over real HTTP. Naming and Arrange/Act/Assert match the neighbors.

### Specs & vault — `.claude/rules/40-specs.md`, `.claude/rules/80-docs-vault.md`
- Every `AC-n` of the implemented spec has a test tagged `// SPEC-NNNN AC-n` (`grep -rn "SPEC-NNNN" app/tests`); list the uncovered ones.
- New dependency/pattern/technology → ADR; new or renamed signal → `docs/03-Architecture/Observability.md`; new domain term → `docs/01-Product/Glossary.md`.

## 5. Report

Classify by impact, not by section:

- **Blockers** — must change before committing: failing build, `ArchitectureTests`, or unit tests; layer violation; endpoint without authorization; identity taken from the request or missing ownership filter; personal data, tokens, or secrets in logs or errors; exception for an expected failure; AI package.
- **Convention violations** — everything else in the checklist (naming, structure, `Result.Failure`, missing event, missing cache invalidation, missing validation rule, incomplete OpenAPI contract…).
- **Test gaps** — missing tests per the Tests and Specs sections.

One line per finding: `file:line` — what's wrong → one-line fix (owning rule). Then:

- **Verification**: each command run and its result.
- **Not verified**: what was skipped and why (e.g. `IntegrationTests` without Docker).
- **Verdict**: ready to commit, or the list of what must change first.
