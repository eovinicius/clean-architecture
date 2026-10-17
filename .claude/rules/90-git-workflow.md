# 90 · Git e Pull Requests (sempre carregada)

**Gitflow.**

| Branch | Sai de | PR para |
| --- | --- | --- |
| `feature/<slug>` (ou `feature/SPEC-NNNN-<slug>`) | `development` | `development` |
| `bugfix/<slug>` | `development` | `development` |
| `release/<x.y.z>` | `development` | `main` (+ back-merge em `development`) |
| `hotfix/<slug>` | `main` | `main` (+ back-merge em `development`) |

- **Nunca** push direto ou force em `main`/`development` (hook `guard-shell.sh` bloqueia). Tudo entra por PR com CI verde.
- Proibido: `--no-verify`, `git reset --hard`, `git clean -f` pelo agente (o usuário executa manualmente se quiser).
- Commit **só quando o usuário pedir**. Conventional Commits com descrição em pt-BR, como no histórico: `feat(todos): adiciona arquivamento de tarefas (SPEC-0001)`. Referencie SPEC/ADR quando houver.
- PR descreve: spec/ADR relacionados, mudança de contrato HTTP (sim/não; breaking?), como foi testado (comandos + resultado), riscos e rollback.
- Não commite artefatos: `bin/`, `obj/`, `.containers/`, `TestResults/`, `.claude/logs/`, `.claude/settings.local.json`.
