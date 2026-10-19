# AGENTS.md — Clean Architecture (.NET 10)

> Contrato para agentes de IA de desenvolvimento (Claude Code, VS Code) e humanos. É carregado em **toda** sessão: mantenha-o curto.
> Detalhes vivem em `.claude/rules/` (por área), `.claude/skills/` (procedimentos) e no vault Obsidian `docs/` (specs, ADRs, runbooks).

## 1. O que é este repositório

API construída sobre o template Clean Architecture: **.NET 10 / C# 14**, monolito modular,
PostgreSQL (EF Core), Serilog + OpenTelemetry exportando por OTLP para o stack local Grafana/Loki/Tempo (LGTM).
O domínio de negócio ainda não foi definido: o código tem só os exemplos do template (`Todos`, `Users`). Não presuma um domínio.

**A aplicação NÃO usa IA em runtime.** Nenhum SDK, cliente ou chave de LLM em `app/`, config ou pacotes (ADR-0003). IA é ferramenta de desenvolvimento.

## 2. Mapa

| Caminho | Conteúdo |
| --- | --- |
| `app/src/SharedKernel` | `Result`/`Result<T>`, `Error`/`ErrorType`, `ValidationError`, `Entity`, `IDomainEvent`, `IDateTimeProvider`. Sem dependências |
| `app/src/Domain` | Entidades, eventos de domínio, catálogos `{Entity}Errors`. Só referencia SharedKernel |
| `app/src/Application` | Casos de uso em vertical slices `{Feature}/{UseCase}/`, `ICommand`/`IQuery` + handlers (sem MediatR), validators, decorators, abstrações (`IApplicationDbContext`, `IUserContext`…) |
| `app/src/Infrastructure` | EF Core + Npgsql (snake_case), migrations em `Database/Migrations`, JWT + refresh tokens, permissões, HybridCache, dispatch de eventos |
| `app/src/Web.Api` | Endpoints minimal API (`IEndpoint`, auto-descobertos), OpenAPI/Scalar, rate limiting, idempotência, `ProblemDetails`, OpenTelemetry |
| `app/tests` | `Application.UnitTests` · `ArchitectureTests` (NetArchTest) · `IntegrationTests` (Testcontainers, exige Docker) |
| `docs/` | Vault Obsidian: `01-Product` (glossário) · `02-Specs` · `03-Architecture` · `04-ADRs` · `05-Engineering` · `06-Runbooks` · `99-Templates` |
| `.claude/` | Harness: `rules/` · `skills/` · `agents/` · `commands/` · `hooks/` · `settings.json` |
| `.mcp.json` | MCP servers de **desenvolvimento**, lidos pelo Claude Code e pelo VS Code (opcionais por dev: `docs/05-Engineering/Agent-Harness.md`) |

## 3. Comandos

```bash
dotnet restore CleanArchitecture.slnx
dotnet format CleanArchitecture.slnx --verify-no-changes --no-restore   # o CI falha se houver diff
dotnet build CleanArchitecture.slnx                                      # warnings já são erros (Directory.Build.props)
dotnet test app/tests/Application.UnitTests                              # rápido, sem Docker (idem ArchitectureTests)
dotnet test CleanArchitecture.slnx                                       # tudo — IntegrationTests exige Docker
docker compose up -d                                                     # Postgres + LGTM (Grafana em http://localhost:3000)
dotnet ef migrations add <Pascal_With_Underscores> --project app/src/Infrastructure --startup-project app/src/Web.Api
bash scripts/agents/verify-harness.sh                                    # valida o próprio harness
```

## 4. Fluxo de trabalho

1. **Feature nova ou mudança de comportamento relevante** → spec `Approved` em `docs/02-Specs/` (skill `write-spec`). Bugfix ou ajuste pequeno dispensa.
2. **Testes** primeiro (citando `// SPEC-NNNN AC-n` quando houver spec) → **código** (skills `add-feature`, `add-entity`, `add-migration`) → build + testes verdes.
3. **Revisão**: `/review` (subagentes `architecture-reviewer` + `security-reviewer`). Spec → `Implemented`.
4. Decisão estrutural nova (dependência, padrão, tecnologia) → **ADR** (skill `create-adr`).

## 5. Regras inegociáveis

- **Camadas** apontam para dentro (SharedKernel ← Domain ← Application ← Infrastructure ← Web.Api); `ArchitectureTests` quebra o build.
- **Erros esperados** = `Result.Failure(...)` com `Error` de catálogo, código `"{EntityPlural}.{Reason}"` (ex.: `TodoItems.NotFound`). Exceção só para o inesperado.
- **CQRS sem MediatR**: handlers `internal sealed`, primary constructor, registrados por Scrutor. Sem registro manual.
- **Dados** só via `IApplicationDbContext`; FK ids, sem navegações; leituras projetam para DTO; listas retornam `PagedList<T>`.
- **Tempo** via `IDateTimeProvider`; usuário via `IUserContext`. Nunca `DateTime.UtcNow` na Application.
- **Endpoints**: `.RequireAuthorization()`/`.HasPermission(...)` sempre; contrato com `.WithSummary` + `.Produces<T>` + `.ProducesProblem`.
- **Pacotes**: versões só em `Directory.Packages.props` (CPM). Pacote novo exige justificativa; de IA é proibido.
- **Migrations** geradas por `dotnet ef`, revisadas, **nunca editadas após merge**.
- **Logs** estruturados, sem PII (dados de usuários e conteúdo livre que eles informam são pessoais — LGPD). **Segredos** nunca em código/config/log/prompt.
- **Git**: Gitflow — `feature/<slug>` a partir de `development`; nunca push direto/force em `main`/`development`; sem `--no-verify`.

## 6. Definition of Done

- [ ] Atende exatamente ao pedido, sem extras fora do escopo; diff revisado linha a linha.
- [ ] `dotnet build` limpo + `Application.UnitTests` e `ArchitectureTests` verdes (+ `IntegrationTests` se tocou persistência/API).
- [ ] Novos caminhos cobertos: happy path e cada `Result.Failure`; ACs da spec com teste.
- [ ] Autorização, propriedade do recurso e invalidação de cache consideradas.
- [ ] Vault atualizado quando aplicável (spec, ADR, `Observability.md`, glossário).
- [ ] Relatório honesto: o que foi feito, o que foi verificado (com resultado) e o que **não** foi.

## 7. Onde aprofundar (carregue sob demanda — não leia tudo de uma vez)

| Preciso de… | Leia |
| --- | --- |
| Princípios gerais de trabalho | `.claude/rules/00-core.md` (resumo) · `docs/05-Engineering/Agent-Guidelines.md` (integral) |
| Camadas, slices, cache | `.claude/rules/10-architecture.md` |
| Estilo C# / analisadores | `.claude/rules/20-csharp.md` |
| Testes | `.claude/rules/30-testing.md` |
| Specs (SDD) | `.claude/rules/40-specs.md`, `docs/02-Specs/_Index.md` |
| Observabilidade | `.claude/rules/50-observability.md`, `docs/03-Architecture/Observability.md` |
| Segurança / LGPD | `.claude/rules/60-security.md` |
| EF Core / migrations | `.claude/rules/70-persistence.md` |
| Vault Obsidian | `.claude/rules/80-docs-vault.md`, `docs/Home.md` |
| Git / PR | `.claude/rules/90-git-workflow.md` |
| Por que algo é assim | `docs/04-ADRs/_Index.md` |
| Termos de negócio | `docs/01-Product/Glossary.md` |
| Como o harness funciona | `docs/05-Engineering/Agent-Harness.md` |

## 8. Comportamento esperado do agente

- Explore pouco e com objetivo: comece por um slice análogo (ex.: `app/src/Application/Todos/Complete/`) e pelo `DependencyInjection.cs` da camada.
- Consulte MCP antes de supor: `microsoft-learn` (APIs .NET 10), `nuget` (versões reais), `context7` (bibliotecas).
- Dúvida de negócio ⇒ pergunte ou registre em `docs/00-Inbox/`; **não invente regra**. Use os termos do glossário.
- Mudanças pequenas e verificáveis; mostre o plano antes de mudanças amplas, breaking ou irreversíveis. Commit só quando pedido.
- Hooks bloqueiam ações perigosas (exit 2): leia a mensagem e ajuste a abordagem — não tente contorná-los.
- Responda em **pt-BR**; código, identificadores e logs em inglês.
