---
id: ADR-0001
title: Registrar decisões arquiteturais no vault
status: Accepted
date: 2026-10-17
tags: [adr, process]
---

# ADR-0001 — Registrar decisões arquiteturais no vault

## Contexto

Decisões estruturais (camadas, dependências, padrões) se perdem em PRs e conversas. Agentes de IA de desenvolvimento precisam saber **por que** algo é como é, para não "corrigir" decisões deliberadas.

## Decisão

Registramos toda decisão estrutural como ADR em `docs/04-ADRs/ADR-NNNN-slug.md`, a partir de `docs/99-Templates/TPL-ADR.md`, com status `Proposed → Accepted → Superseded/Deprecated`.

## Alternativas

- **Wiki externa**: fora do repositório, sem revisão em PR e invisível aos agentes. Rejeitada.
- **Só descrição de PR**: difícil de encontrar e sem estado. Rejeitada.

## Consequências

- Positivas: histórico versionado junto do código; agentes leem sob demanda.
- Negativas: disciplina de manter o índice e de não editar ADR aceita.

## Como verificar

Hook `validate-docs.sh` valida o frontmatter das ADRs; `protect-files.sh` pede confirmação ao editar ADR `Accepted`.
