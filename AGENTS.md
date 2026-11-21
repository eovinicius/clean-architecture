# AGENTS.md — Clean Architecture (.NET 10)

> Contrato para agentes de IA (Claude Code, VS Code) e humanos, carregado em **toda** sessão: mantenha-o curto.
> Detalhes sob demanda: `.claude/rules/` (por área), `.claude/skills/` (procedimentos) e vault `docs/` (§7).

## 1. Produto

- API sobre o template Clean Architecture: **.NET 10 / C# 14**, monolito modular, PostgreSQL (EF Core), Serilog + OpenTelemetry via OTLP para o LGTM local (Grafana/Loki/Tempo).
- Domínio de negócio indefinido: só os exemplos do template (slices em `app/src/Application/`). Não presuma domínio nem invente regra — pergunte ou registre em `docs/00-Inbox/`; use os termos do glossário.
- **Sem IA em runtime**: nenhum SDK, cliente, pacote ou chave de LLM em `app/`, config ou `Directory.Packages.props`. IA é só ferramenta de desenvolvimento.

## 2. Mapa

| Caminho | Conteúdo |
| --- | --- |
| `app/src/SharedKernel` | `Result`/`Result<T>`, `Error`/`ErrorType`, `ValidationError`, `Entity`, `IDomainEvent`, `IDateTimeProvider`; sem dependências |
| `app/src/Domain` | Entidades, eventos de domínio (opcionais), catálogos `{Entity}Errors`; só referencia SharedKernel |
| `app/src/Application` | Vertical slices `{Feature}/{UseCase}/`: `ICommand`/`IQuery` + handlers, validators, decorators, abstrações (`I{Entity}Repository`, `IUnitOfWork`, `IUserContext`…); sem EF Core |
| `app/src/Infrastructure` | EF Core + Npgsql (snake_case), repositories e `UnitOfWork` em `Database/`, configurações EF em `Database/Mappings`, migrations em `Database/Migrations`, JWT + refresh tokens, permissões, HybridCache, dispatch de eventos |
| `app/src/Api` | Controllers por feature (`Controllers/{Feature}Controller.cs`), OpenAPI/Scalar, rate limiting, idempotência, `ProblemDetails`, OpenTelemetry |
| `app/tests` | `Application.UnitTests` · `ArchitectureTests` (NetArchTest) · `IntegrationTests` (Testcontainers) |
| `docs/` | Vault Obsidian: `01-Product` (glossário) · `02-Specs` · `03-Architecture` · `05-Engineering` · `06-Runbooks` · `99-Templates` |
| `.claude/` · `.mcp.json` | Harness (`rules/` · `skills/` · `agents/` · `commands/` · `hooks/` · `settings.json`) · MCP de desenvolvimento, lido pelo Claude Code e pelo VS Code |

## 3. Comandos

```bash
dotnet restore CleanArchitecture.slnx
dotnet format CleanArchitecture.slnx --verify-no-changes --no-restore   # o CI falha se houver diff
dotnet build CleanArchitecture.slnx                                      # warnings já são erros (Directory.Build.props)
dotnet test app/tests/Application.UnitTests                              # rápido, sem Docker (idem ArchitectureTests)
dotnet test CleanArchitecture.slnx                                       # tudo — IntegrationTests exige Docker
docker compose up -d                                                     # Postgres + LGTM (Grafana em http://localhost:3000)
dotnet ef migrations add <Pascal_With_Underscores> --project app/src/Infrastructure --startup-project app/src/Api
bash scripts/agents/verify-harness.sh                                    # valida o próprio harness
```

## 4. Invariantes

- **Camadas** apontam para dentro (SharedKernel ← Domain ← Application ← Infrastructure ← Api); `ArchitectureTests` quebra o build.
- **Erros esperados** = retorne o `Error` de catálogo direto (`return {Entity}Errors.NotFound(id);` — conversão implícita para `Result`/`Result<T>`; nada de `Result.Failure<T>(...)` em handler/entidade), código `"{EntityPlural}.{Reason}"`. Exceção só para o inesperado.
- **Domínio rico**: estado só muda por método da entidade (`private set`, factory `Create`), que valida a regra, retorna `Result` e, quando a feature usa eventos, levanta o evento; handler só orquestra. Classe/record concreto é `sealed` salvo se for base de herança. `ConventionTests` quebra o build.
- **CQRS sem MediatR**: handlers `internal sealed`, primary constructor, registrados por Scrutor — sem registro manual.
- **Dados** só via `I{Entity}Repository` + `IUnitOfWork` (Application sem EF Core); FK ids, sem navegações; leituras mapeiam para DTO; listas retornam `PagedList<T>`.
- **Tempo** via `IDateTimeProvider`, usuário via `IUserContext` — nunca `DateTime.UtcNow` na Application.
- **Endpoints**: controller `[Authorize]` (anônimo só com `[AllowAnonymous]` justificado; permissão com `[HasPermission(...)]`); contrato com `[EndpointSummary]` + `[ProducesResponseType<T>]` + `[ProducesResponseType<ProblemDetails>]`.
- **Pacotes**: versões só em `Directory.Packages.props` (CPM); pacote novo exige justificativa; de IA, proibido (§1).
- **Migrations** geradas por `dotnet ef`, revisadas, **nunca editadas após merge**.
- **Logs** estruturados, sem PII (dados de usuários e o conteúdo livre que informam são pessoais — LGPD). **Segredos** nunca em código/config/log/prompt.
- **Git**: Gitflow — `feature/<slug>` a partir de `development`; nunca push direto/force em `main`/`development`; sem `--no-verify`.

## 5. Fluxo

1. Feature nova ou mudança relevante de comportamento → spec `Approved` em `docs/02-Specs/` (skill `write-spec`); bugfix ou ajuste pequeno dispensa.
2. Testes primeiro (ao menos um por AC) → código (skills `add-feature`, `add-entity`, `add-migration`).
3. `/review` (`architecture-reviewer` + `security-reviewer`) → spec `Implemented`.

## 6. Definition of Done

- [ ] Exatamente o pedido, sem extras; diff revisado linha a linha.
- [ ] `dotnet build` limpo + `Application.UnitTests` e `ArchitectureTests` verdes (+ `IntegrationTests` se tocou persistência/API).
- [ ] Happy path e cada `Result.Failure` novos cobertos; cada AC da spec com teste.
- [ ] Autorização, propriedade do recurso e invalidação de cache consideradas.
- [ ] Vault atualizado quando aplicável (spec, `Observability.md`, glossário).
- [ ] Relatório honesto: o que foi feito, o que foi verificado (com resultado) e o que **não** foi.

## 7. Onde aprofundar (sob demanda, não tudo de uma vez)

- Rules em `.claude/rules/`: `core` (princípios; integral em `docs/05-Engineering/Agent-Guidelines.md`) · `architecture` · `csharp` · `testing` · `specs` · `observability` · `security` (LGPD) · `persistence` · `docs-vault` · `git-workflow`.
- Vault: `docs/Home.md` · `docs/02-Specs/_Index.md` · `docs/03-Architecture/Observability.md` · `docs/01-Product/Glossary.md` · `docs/05-Engineering/Agent-Harness.md` (harness e MCP opcionais por dev).

## 8. Como agir

- Explore pouco: comece por um slice análogo em `app/src/Application/{Feature}/{UseCase}/` e pelo `DependencyInjection.cs` da camada; consulte MCP antes de supor — `microsoft-learn` (APIs .NET 10), `nuget` (versões reais), `context7` (bibliotecas).
- Mudanças pequenas e verificáveis; plano antes de mudança ampla, breaking ou irreversível; commit só quando pedido.
- Hook bloqueou (exit 2)? Leia a mensagem e ajuste a abordagem — não contorne.
- Responda em **pt-BR**; código, identificadores e logs em inglês.

<!-- super-board:begin v3.1.2 (managed; edits inside are overwritten on upgrade) -->
## Super Board

Board: Backlog · Ready · Building · QA · Review · Blocked · Done. Labels route: `qa` skips Building · `bug`, `feature`, none → built first.

| When | Use | NEVER |
|---|---|---|
| Set up / repair the board | `/super-board onboard` | DON'T hand-edit config mid-run |
| Check tickets before a run | `/super-board lint` | NEVER run with AC-less tickets |
| Drain the board | `/super-board run` | NEVER build, test or merge from the orchestrator |
| Board state / halt | `/super-board status` · `/super-board stop` | |
| Build one ticket | `/super-build` | NEVER build outside the card's worktree |
| QA a branch, or a live URL alone | `/super-qa` · `/super-qa <url>` | NEVER mark QA pass without evidence |
| Review a PR, merge | `/super-review` | NEVER `gh pr merge` direct — merge gate only |
| File bugs from Sentry, PostHog, PRs | `/super-collect` | DON'T file without a verifier pass |
| UI polish | `/ui-refine-loop` (human runs it) | board NEVER runs it |
| Diagram / explainer page | `/visual` | |
| Commit, pull, push this branch | `/git-sync` | NEVER force push or rebase shared history |

| Rule | Value |
|---|---|
| Config | `.claude/super-board/configs/<slug>.json` |
| Isolation | 1 card · 1 worktree · 1 branch |
| Merge | auto for normal changes · money, auth, destructive schema (DROP/TRUNCATE/RENAME) → human · PR > 400 changed lines → human |
| Migrations | additive → robot migrates allowed DBs only · live DB ALWAYS → 🙋 needs you |
| Blocked card | block-template comment · last line `blocked-by:` · 🙋 = needs you |
| Secrets | NEVER read `.env`; key names via `.claude/bin/super-board-env-check.sh` |

## Writing (super-board)

| Thing | Format → `.claude/skills/super-board/references/writing-standard.md` |
|---|---|
| Commit, PR title | `<emoji> [type] scope: subject` + short bullets · ✨ feat 🐛 fix 🔧 chore ♻️ refactor 🧪 test 📝 docs |
| PR body | blocks: status · Problem · Solution · AC + proof · history · Before\|After · Risk — `super-board-pr-body.sh` |
| Ticket | Problem · Context · Fix · AC · Risk · Blocked by → `docs/agents/issue-tracker.md` |
| Comment | `[role] [label] status` · Did · ✅ Done · ❌ Not done · Next · ≤ 8 lines |
| PR author notes | file + critical inline review comments: Purpose · What changed · Why it matters → `pr-author-notes.md` |

- NEVER chain steps with arrows. One step per line, lettered under Where.
- NEVER link screenshots. Embed a raw URL pinned to a sha.
- DON'T repeat file lists in status comments. DON'T write "Not verified" or "Next" in a PR body.
<!-- super-board:end -->
