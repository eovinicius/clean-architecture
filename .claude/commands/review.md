---
description: Revisão completa do branch atual (arquitetura + segurança) antes do PR
argument-hint: '[escopo opcional, ex.: app/src/Application/Todos]'
---

Revise o branch atual contra `development` (em `hotfix/*`, contra `main`). Escopo: $ARGUMENTS (se vazio, todo o diff, incluindo mudanças não commitadas).

1. Invoque **em paralelo** os subagentes `architecture-reviewer` e `security-reviewer` com o mesmo escopo.
2. Consolide os achados em uma única lista priorizada (bloqueantes primeiro), sem duplicatas, com `arquivo:linha`.
3. Informe a verificação que os revisores executaram e o que ficou sem verificar (ex.: integração sem Docker).
4. Sugira o texto da descrição do PR seguindo `.claude/rules/git-workflow.md` (spec/ADR, contrato HTTP alterado, como foi testado, riscos e rollback).

Não corrija nada nem faça commit: só reporte.
