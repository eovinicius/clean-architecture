---
id: SPEC-NNNN
title: <título curto da funcionalidade>
status: Draft
tags: [spec]
adrs: []
created: {{date}}
updated: {{date}}
---

# SPEC-NNNN — <título>

## 1. Problema

Quem sofre, qual dor, por que agora. Métrica de sucesso mensurável.

## 2. Escopo

- **Inclui**:
- **Não inclui**:

## 3. Atores e termos

Atores e termos do [[Glossary]] envolvidos. Termo novo ⇒ adicionar ao glossário.

## 4. Regras de negócio

- **RN-1**:
- **RN-2**:

## 5. Critérios de aceite

- **AC-1** — Dado …, quando …, então … (HTTP 200).
- **AC-2** — Dado …, quando …, então falha com `"{EntityPlural}.{Reason}"` (HTTP 4xx).
- **AC-3** — Autorização: dado um usuário sem permissão, quando …, então HTTP 403; dado um recurso de outro usuário, quando …, então HTTP 404 (`{Entity}Errors.NotFound`, não revela que o recurso existe).

## 6. Contrato HTTP

| Operação | Método e rota | Request | Response | Erros |
| --- | --- | --- | --- | --- |
| | | | | |

Mudança em endpoint existente é breaking? (sim/não — se sim, explique.)

## 7. Requisitos não funcionais

Autorização/permissões, volume esperado, cache, sinais de observabilidade novos (`app.*`), dados pessoais (LGPD).

## 8. Perguntas em aberto

- [ ]

## 9. Rastreabilidade

Testes: `grep -rn "SPEC-NNNN" app/tests`.
