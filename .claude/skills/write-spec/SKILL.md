---
name: write-spec
description: Cria ou evolui uma spec funcional (SDD) no vault Obsidian em docs/02-Specs, com regras de negócio e critérios de aceite testáveis no vocabulário do glossário. Use quando o usuário pedir uma feature nova, uma mudança de regra de negócio ou de endpoint, ou quando não existir spec Approved para o que foi pedido.
argument-hint: <descrição da feature, ou SPEC-NNNN para evoluir>
---

# write-spec

Transforma um pedido em uma spec **revisável**, que humanos e agentes usam como contrato. **Não escreva código nesta skill.** Regras de formato: `.claude/rules/40-specs.md`.

## Passos

1. **Contexto mínimo** (não leia o repositório inteiro):
   - `docs/01-Product/Glossary.md` e `docs/02-Specs/_Index.md`.
   - Specs e ADRs relacionados: `grep -ril "<termo>" docs/02-Specs docs/04-ADRs`.
   - Se a feature toca código existente, só o slice/entidade afetado (ex.: `app/src/Application/{Feature}/`, `app/src/Domain/{Feature}/`), para descrever o comportamento atual sem adivinhar.
2. **ID**: maior `SPEC-NNNN` em `docs/02-Specs/` + 1 (primeira = `SPEC-0001`). Arquivo `docs/02-Specs/SPEC-NNNN-<slug-kebab>.md` a partir de `docs/99-Templates/TPL-Spec.md`; troque `{{date}}` pela data de hoje e use `status: Draft`.
3. **Preencha** as seções 1–7 e 9:
   - `RN-n` atômicas, no vocabulário do glossário. Termo novo ⇒ entrada nova no glossário (marcada "a confirmar" se a definição não veio do usuário).
   - `AC-n` em *Dado/Quando/Então*, cada um verificável por teste automatizado. Inclua o erro principal (código `"{FeaturePlural}.{Reason}"` + status HTTP) e a autorização (403/404).
   - Seção 6: operação, rota, request/response, erros; diga se altera endpoint existente de forma breaking.
   - Seção 7: permissões, cache, sinais `app.*` novos, dados pessoais envolvidos.
4. **Perguntas em aberto** (seção 8): tudo o que você precisou supor. **Não invente regra de negócio** — pergunte.
5. Atualize a tabela de `docs/02-Specs/_Index.md`.
6. **Revisão**: invoque o subagente `spec-reviewer`, aplique os achados `[bloqueante]` e mude o status para `Review`.
7. Resuma para o usuário: ID, caminho, perguntas em aberto que bloqueiam `Approved`. Só o humano aprova (`status: Approved`).

## Evoluir uma spec existente

Leia a spec inteira; mantenha a numeração existente (não renumere `RN`/`AC`: os testes citam esses IDs). Critério removido vira ~~riscado~~ com motivo; critério novo recebe o próximo número. Atualize `updated:`.
