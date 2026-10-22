# 90 · Git e Pull Requests (sempre carregada)

**Gitflow.**

| Branch | Sai de | PR para |
| --- | --- | --- |
| `feature/<slug>` (ou `feature/SPEC-NNNN-<slug>`) | `development` | `development` |
| `bugfix/<slug>` | `development` | `development` |
| `release/<x.y.z>` | `development` | `main` (+ back-merge em `development`) |
| `hotfix/<slug>` | `main` | `main` (+ back-merge em `development`) |

- **Base atualizada antes de criar a branch**: `git switch development && git pull --ff-only && git switch -c feature/<slug>` (`main` para `hotfix/*`). Working tree sujo ou `pull` que não é fast-forward ⇒ pare e avise o usuário; não faça stash, merge nem rebase por conta própria. O hook `guard-shell.sh` bloqueia branch criada de outra base ou de base atrás de `origin`.
- **Nunca** push direto ou force em `main`/`development` (hook `guard-shell.sh` bloqueia). Tudo entra por PR com CI verde — depois de abrir/atualizar um PR, acompanhe com o subagente `ci-watcher`.
- Proibido: `--no-verify`, `git reset --hard`, `git clean -f` pelo agente (o usuário executa manualmente se quiser).
- Commit **só quando o usuário pedir**. Conventional Commits com descrição em pt-BR, como no histórico: `feat(todos): adiciona arquivamento de tarefas (SPEC-0001)`. Referencie SPEC/ADR quando houver.
- PR descreve: spec/ADR relacionados, mudança de contrato HTTP (sim/não; breaking?), como foi testado (comandos + resultado), riscos e rollback.
- Não commite artefatos: `bin/`, `obj/`, `.containers/`, `TestResults/`, `.claude/logs/`, `.claude/settings.local.json`.
