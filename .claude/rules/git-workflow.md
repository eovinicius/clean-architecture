# Git e Pull Requests (sempre carregada)

**Gitflow.**

| Branch | Sai de | PR para |
| --- | --- | --- |
| `feature/<slug>` (ou `feature/SPEC-NNNN-<slug>`) | `development` | `development` |
| `bugfix/<slug>` | `development` | `development` |
| `release/<x.y.z>` | `development` | `main` (+ back-merge em `development`) |
| `hotfix/<slug>` | `main` | `main` (+ back-merge em `development`) |
| `loop/issue-<N>` (super-board, worktree `.claude/worktrees/issue-<N>`) | `development` | `development` |

- **Base atualizada antes de criar a branch**: `git switch development && git pull --ff-only && git switch -c feature/<slug>` (`main` para `hotfix/*`). Working tree sujo ou `pull` que não é fast-forward ⇒ pare e avise o usuário; não faça stash, merge nem rebase por conta própria. O hook `guard-shell.sh` bloqueia branch criada de outra base ou de base atrás de `origin`.
- **Nunca** push direto ou force em `main`/`development` (hook `guard-shell.sh` bloqueia). Tudo entra por PR com CI verde — depois de abrir/atualizar um PR, acompanhe com o subagente `ci-watcher`.
- Proibido: `--no-verify`, `git reset --hard`, `git clean -f` pelo agente (o usuário executa manualmente se quiser).
- Commit **só quando o usuário pedir**. Conventional Commits com descrição em pt-BR, como no histórico: `feat(<escopo>): <descrição em pt-BR> (SPEC-NNNN)`. Referencie a SPEC quando houver.
- PR descreve: spec relacionada, mudança de contrato HTTP (sim/não; breaking?), como foi testado (comandos + resultado), riscos e rollback.
- Não commite artefatos: `bin/`, `obj/`, `.containers/`, `TestResults/`, `.claude/settings.local.json`.

## Super-board (lanes de um card)

Num card do board, o próprio card é o pedido do usuário. As regras acima continuam valendo (sem force, sem push em `main`/`development`, sem `--no-verify`, sem `reset --hard`/`clean -f`), com estas trocas:

- Commit, push, PR e comentários na branch do card **sem pedir confirmação**; merge **só** pelo `super-board-merge-gate.sh` (nunca `gh pr merge` direto). Merge/rebase da base na branch do card para resolver conflito é permitido.
- Commit, título e corpo de PR seguem o padrão do super-board (seção *Writing* do `AGENTS.md` e `super-board-pr-body.sh`), não o Conventional Commits nem o formato de PR acima.
- O CI é acompanhado pelas lanes de QA/Review do próprio super-board — não chame o `ci-watcher`.
