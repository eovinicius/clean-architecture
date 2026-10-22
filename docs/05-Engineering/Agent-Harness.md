---
title: Agent Harness
status: Approved
tags: [engineering, ai, harness]
---

# Agent Harness

O **harness** é o conjunto versionado de artefatos que transforma um agente genérico (Claude Code CLI, VS Code) em um colaborador que conhece *este* repositório. Decisão: [[ADR-0003-sdd-and-agent-harness]]. Diretrizes integrais: [[Agent-Guidelines]].

## Princípio: contexto é orçamento

O desempenho do agente é limitado pela qualidade do contexto, não pelo tamanho do modelo. Cada token irrelevante dilui a atenção. Por isso projetamos **o que** entra, **quando** e **de onde**:

| Camada | Mecanismo | Regra de ouro |
| --- | --- | --- |
| 1. Identidade e invariantes | `AGENTS.md` (importado pelo `CLAUDE.md`) | Curto, estável, só o que vale para **toda** tarefa |
| 2. Conhecimento por área | `.claude/rules/*.md` com `paths:` | Carrega ao tocar a área — nunca "tudo sempre" |
| 3. Procedimentos | `.claude/skills/*/SKILL.md` | Descobertos pela `description`; corpo só quando usado |
| 4. Estado dinâmico | Hooks `SessionStart` / `UserPromptSubmit` | Ponteiros (caminho + status), não conteúdo |
| 5. Conhecimento externo vivo | MCP (`.mcp.json`) | Consultar em vez de "lembrar" APIs e versões |
| 6. Memória de longo prazo | Vault `docs/` | Specs e ADRs como fonte; lidos sob demanda |
| 7. Isolamento | Subagentes `.claude/agents/` | Revisões e buscas amplas fora do contexto principal |
| 8. Garantias | Hooks bloqueantes + build + testes | O que a máquina pode verificar **não** fica só em texto |

```mermaid
flowchart LR
    A[AGENTS.md] --> CC[Claude Code]
    A --> VS[VS Code]
    CL[CLAUDE.md] --> CC
    CL --> VS
    R[.claude/rules] --> CC
    R --> VS
    S[.claude/skills] --> CC
    S --> VS
    G[.claude/agents] --> CC
    H[.claude/hooks + settings.json] --> CC
    M[.mcp.json] --> CC
    M --> VS
    D[docs/ vault] -.sob demanda.-> CC
    D -.sob demanda.-> VS
```

### O que o VS Code aproveita

- Lê `AGENTS.md` e `CLAUDE.md` (`chat.useAgentsMdFile` e `chat.useClaudeMdFile` em `.vscode/settings.json`), `.claude/rules/` respeitando `paths:`, `.claude/skills/` e o `.mcp.json` da raiz.
- Subagentes e `/review` são do Claude Code.
- Hooks em formato Claude existem no VS Code só em **preview** (`chat.useClaudeHooks`, desligado). Lá os matchers são ignorados e os nomes/campos das ferramentas diferem: `session-start.sh` e `stop-verify.sh` funcionariam, mas os guardas de shell e de arquivos não reconhecem a entrada e saem com 0. Os guardrails completos valem só no Claude Code.

## Componentes

| Artefato | Papel |
| --- | --- |
| `AGENTS.md` | Contrato agnóstico: mapa, comandos, fluxo, regras inegociáveis, DoD, onde aprofundar |
| `CLAUDE.md` | `@AGENTS.md` + notas específicas do Claude Code + regra de idioma |
| `.claude/rules/` | `00-core`, `60-security`, `90-git-workflow` sempre; demais por `paths:` (`85-harness` ao mexer no próprio harness) |
| `.claude/skills/` | Template: `add-feature`, `add-entity`, `add-migration`, `add-tests`, `add-background-job`, `bootstrap`, `ca-review`. Harness: `write-spec`, `implement-spec`, `create-adr`, `dependency-upgrade`, `instrument-observability`, `investigate-issue` |
| `.claude/agents/` | `architecture-reviewer`, `security-reviewer`, `code-reviewer`, `spec-reviewer`, `ci-watcher` (somente leitura) e `test-engineer` |
| `.claude/commands/review.md` | `/review`: dois revisores em paralelo + texto de PR |
| `.claude/hooks/` | Guardrails determinísticos (tabela abaixo) |
| `scripts/agents/verify-harness.sh` | Verifica o próprio harness (local e CI) |

## Hooks

Scripts bash (no Windows, o Claude Code usa o Git Bash). Contrato: `exit 0` segue; `exit 2` bloqueia e o stderr volta ao agente como instrução corretiva. JSON lido com `jq` → `node` → `python` → fallback bash. Decisões registradas em `.claude/logs/hooks.log` (gitignored, com credenciais mascaradas e rotação acima de 512 KB).

| Script | Evento | Faz | Bloqueia? |
| --- | --- | --- | --- |
| `session-start.sh` | SessionStart | Branch, commits à frente de `development`, alterações, SDK, Docker, specs ativas | não |
| `prompt-context.sh` | UserPromptSubmit | Bloqueia segredos no prompt; resolve `SPEC-/ADR-/RB-` citados em caminho + status | sim (segredo) |
| `guard-shell.sh` | PreToolUse (Bash, PowerShell) | `rm`/`Remove-Item` destrutivo, push em `main`/`development` (inclusive `git -C`, `HEAD` e sem refspec), force, branch do Gitflow criada fora da base (`development`; `main` para `hotfix/*`) ou com a base atrás de `origin` (`ls-remote`; offline, último fetch), `--no-verify`/`commit -n`, descarte de trabalho (`reset --hard`, `checkout .`, `stash drop`), EF contra host remoto, qualquer acesso a arquivos de segredo, pacotes de IA e `--version` com CPM (`dotnet add package` e `dotnet package add`), `curl \| sh`/`iex (irm …)`. Avalia cada segmento do comando; falha fechado se não conseguir ler o JSON | sim |
| `readonly-shell.sh` | PreToolUse (Bash) no frontmatter de `architecture-reviewer`, `security-reviewer`, `code-reviewer` e `ci-watcher` | Allowlist de leitura (`git diff/log/status…`, `gh pr/run` de consulta, `dotnet test/build/list`, `grep`, `cat`…); bloqueia redirecionamento para arquivo, `$(...)`, `sed -i`, `find -delete` e o que estiver fora da lista. Divide o comando respeitando aspas | sim |
| `protect-files.sh` | PreToolUse (Edit, Write) | `.env`, chaves, `.git/`, `.containers/`, snapshot do EF, migrations já em `main`/`development` (locais ou `origin/`); confirmação para ADR `Accepted`. Não cobre escrita via shell (`echo >`, `sed -i`) | sim / pergunta |
| `format-dotnet.sh` | PostToolUse (Edit, Write) | `dotnet format whitespace` nos `.cs` editados | não |
| `validate-docs.sh` | PostToolUse (Edit, Write) | Frontmatter, ID e status das notas do vault | sim |
| `stop-verify.sh` | Stop | Se código .NET mudou: build + `Application.UnitTests` + `ArchitectureTests` (orçamento de 540 s, abaixo do timeout do hook). Re-verifica a cada tentativa de parar; após 3 bloqueios seguidos, libera com aviso ao usuário. Opt-out: `HARNESS_SKIP_STOP_VERIFY=1` | sim |

**Git hook (humanos e agentes):** `.githooks/pre-commit` roda `dotnet format whitespace --verify-no-changes` nos `.cs` staged e recusa o commit se houver diff — a mesma falha que o passo *Verify formatting* do CI pegaria minutos depois. Ative uma vez por clone com `git config core.hooksPath .githooks`.

## MCP servers (desenvolvimento apenas)

Um único `.mcp.json` na raiz, lido pelo Claude Code **e** pelo VS Code (que também reconhece o `.mcp.json` da raiz). Ele só tem servidores sem segredo e sem pré-requisito além do SDK/Node:

| Servidor | Uso | Requisito |
| --- | --- | --- |
| `microsoft-learn` | APIs .NET 10 / ASP.NET Core / EF Core | nenhum (HTTP público) |
| `context7` | Docs versionadas de bibliotecas | Node (`npx`) |
| `nuget` | Versões reais e vulnerabilidades de pacotes | .NET 10 SDK (`dnx`) |

### Opcionais, por dev (escopo local, nunca commitados)

Exigem chave ou ferramenta local. Por isso não ficam no `.mcp.json`, que valeria para o time inteiro. No Claude Code, `--scope local` grava só para você neste projeto; no VS Code, use **MCP: Add Server** no escopo de usuário.

```bash
# Obsidian — busca no vault com o Obsidian aberto (plugin "Local REST API"; requer uv)
claude mcp add obsidian --scope local -e OBSIDIAN_API_KEY=<chave-do-plugin> -- uvx mcp-obsidian

# Postgres LOCAL do docker-compose em modo restrito (explain, índices); nunca aponte para outro ambiente
claude mcp add postgres-local --scope local \
  -e DATABASE_URI=postgresql://postgres:postgres@localhost:5432/clean-architecture \
  -- uvx postgres-mcp --access-mode=restricted

# Grafana LGTM local (logs/traces/métricas para a skill investigate-issue); token de service account do Grafana local
claude mcp add grafana --scope local -e GRAFANA_URL=http://host.docker.internal:3000 -e GRAFANA_SERVICE_ACCOUNT_TOKEN=<token> \
  -- docker run --rm -i -e GRAFANA_URL -e GRAFANA_SERVICE_ACCOUNT_TOKEN grafana/mcp-grafana -t stdio

# GitHub (PRs, issues, Actions) com PAT fine-grained restrito a este repositório.
# Exporte GITHUB_PAT antes (sem colar o token na linha de comando, que vai para o histórico do shell).
claude mcp add github --scope local --transport http https://api.githubcopilot.com/mcp/ \
  --header "Authorization: Bearer $GITHUB_PAT"
```

O `mcp-grafana` roda em container, por isso usa `host.docker.internal` (Docker Desktop) para alcançar o Grafana em `localhost:3000`.

## Higiene de sessão

- Uma tarefa (ou uma spec) por sessão; `/clear` entre tarefas não relacionadas.
- Ao compactar, preserve: spec em andamento, ACs pendentes, arquivos alterados, decisões e verificações pendentes.

## Anti-padrões

- Colar specs/ADRs inteiras no `AGENTS.md`.
- Regras vagas ("escreva código limpo") sem ação verificável.
- MCPs demais habilitados por padrão (ferramentas também consomem contexto).
- A mesma regra duplicada em vários arquivos (diverge com o tempo) — cada regra tem um dono.

## Manutenção

1. Mudou uma convenção? Atualize **um** lugar (rule ou skill) e, se for invariante, a linha correspondente do `AGENTS.md`.
2. `bash scripts/agents/verify-harness.sh` antes do PR (o CI também roda).
3. Revisão trimestral: rules obsoletas, skills sem uso (`.claude/logs/hooks.log`), tamanho do `AGENTS.md`.
4. Maturidade externa: `npx harness-score`. O `.harness-score.json` desliga `SNS-01`, `SNS-02` e `SNS-04` porque, até a v1.8.3, essas checagens só reconhecem ferramentas de Node/Python/Go/Rust/Java/PHP. Os sensores equivalentes existem e o CI os executa: `dotnet test` (test runner), analisadores com `AnalysisMode=All` + `TreatWarningsAsErrors` + SonarAnalyzer (linter) e `.editorconfig` + `dotnet format` (formatter, também no `.githooks/pre-commit`). Se uma versão nova passar a detectar .NET, remova as exceções.
