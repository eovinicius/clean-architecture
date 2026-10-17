---
id: ADR-0003
title: SDD no vault, Agent Harness e nenhuma IA em runtime
status: Proposed
date: 2026-10-17
tags: [adr, process, ai]
---

# ADR-0003 — SDD no vault, Agent Harness e nenhuma IA em runtime

## Contexto

O time desenvolve com agentes de IA (Claude Code CLI e VS Code). Sem contexto curado, agentes inventam regras, ignoram convenções e gastam contexto lendo o repositório inteiro. A aplicação **não** tem requisito de IA em runtime e lida com dados pessoais (LGPD).

## Decisão

1. **Nenhuma IA em runtime**: nenhum SDK, cliente, pacote ou chave de LLM em `app/`, `Directory.Packages.props` ou configuração.
2. **Spec-Driven Development leve** no vault: features relevantes começam por `docs/02-Specs/SPEC-NNNN` com critérios de aceite testáveis; testes citam `// SPEC-NNNN AC-n`.
3. **Agent Harness versionado**: `AGENTS.md` (contrato curto), `.claude/rules` (por área), skills, subagentes, hooks determinísticos e MCPs de desenvolvimento ([[Agent-Harness]]).

## Alternativas

- **OpenSpec**: avaliado e removido por enquanto; adiciona uma CLI e uma segunda árvore de specs fora do vault.
- **Só instruções em texto, sem hooks**: regras críticas (segredos, push em branches protegidas, migrations mergeadas) dependeriam da obediência do modelo. Rejeitada.

## Consequências

- Positivas: contexto previsível e barato; guardrails verificáveis por máquina; decisões rastreáveis.
- Negativas: manutenção do harness (revisão periódica de rules e skills) e disciplina de manter specs.

## Como verificar

- `bash scripts/agents/verify-harness.sh` (também no CI) falha se houver pacote de IA no runtime ou hook quebrado.
- O hook `guard-shell.sh` bloqueia `dotnet add package` de bibliotecas de IA.
