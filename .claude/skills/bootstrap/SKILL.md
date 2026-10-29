---
name: bootstrap
description: Turn this template into a fresh project — rename the solution and (optionally) namespaces to the user's app name, and remove the sample Todos feature so they start clean. Use right after cloning when the user wants to begin their own project from the template.
argument-hint: <new project/solution name, e.g. "Acme.Billing">
disable-model-invocation: true
---

# Bootstrap a New Project from the Template

Two jobs: **(1)** rename the template to the user's name, and **(2)** strip the sample **Todos** feature (keep Users + auth — most apps need them). Confirm the new name with the user first.

## 1. Rename

The template ships as the `CleanArchitecture` solution with a `Web.Api` app and `Domain`/`Application`/`Infrastructure`/`SharedKernel` projects.

- Rename `CleanArchitecture.slnx` → `{Name}.slnx`.
- If the user wants a namespace prefix (e.g. `Acme.Billing.Domain`), rename each project folder + `.csproj` + its root namespace + every `using`, and the `<ProjectReference>`/`InternalsVisibleTo` entries. If they're fine with the plain layer names (`Domain`, `Application`, …), skip this.
- Replace the template's hardcoded identifiers: the database name and `Jwt:Issuer`/`Audience` in `appsettings*.json`, the `POSTGRES_DB`/service names + `Host=` in `docker-compose.yml`, the `README.md` title, and any `UserSecretsId` if they want a fresh one.

## 2. Remove the Todos sample

Delete the Todos slice everywhere:

- `app/src/Domain/Todos/`, `app/src/Application/Todos/`, `app/src/Infrastructure/Todos/`, `app/src/Web.Api/Endpoints/Todos/`
- `app/tests/Application.UnitTests/Todos/`, `app/tests/IntegrationTests/Todos/`
- Remove the `TodoItems` `DbSet` from `IApplicationDbContext`, `ApplicationDbContext`, and `app/tests/Application.UnitTests/Abstractions/TestDbContext.cs`; delete `TodoItemConfiguration`.
- Drop the table with a migration (`/add-migration Remove_Todos`), or — for a brand-new database — delete `app/src/Infrastructure/Database/Migrations/`, `dotnet ef migrations add Initial`, and let it recreate.

## 3. Re-point the harness

Rules, skills and agents use the Todos slices as **canonical models** (e.g. `Todos/Complete/`, `Todos/GetById/`, `TodoCacheKeys`). After step 2 they point at files that no longer exist.

- Run `bash scripts/agents/verify-harness.sh`: the "caminhos citados" check lists every broken reference with the file that cites it.
- For each one, point to the equivalent model that still exists (usually in `Users/`) — edit only the file that owns the convention (`.claude/rules/harness.md`). If there is no equivalent (e.g. the ownership model `Todos/GetById/`), drop the "model:" pointer, keep the rule text, and tell the user; don't invent a new model.
- If the solution was renamed, replace `CleanArchitecture.slnx` in `AGENTS.md`, `.claude/` and `scripts/agents/` (`grep -rn "CleanArchitecture.slnx"`) — `stop-verify.sh` builds it by name.
- Finish with `verify-harness.sh` at exit 0.

## Rules

- Keep the whole SharedKernel/Domain/Application/Infrastructure/Web.Api structure and every cross-cutting concern (auth, caching, endpoints, rate limiting, security headers, observability) — only the *sample feature* is removed.
- Build + `dotnet test` after each stage; ArchitectureTests and the integration tests surface any dangling reference.
- Leave the `.claude/skills/` pack in place so the conventions travel with the new project.
