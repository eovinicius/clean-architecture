---
id: RB-NNN
title: <procedimento>
status: Draft
tags: [runbook]
---

# RB-NNN — <procedimento>

## Quando usar

Sintoma, alerta ou situação que dispara este runbook.

## Diagnóstico

1. Onde olhar (Grafana: consulta Loki/Tempo/Prometheus, comando, endpoint de health).
2. Como confirmar a hipótese.

## Mitigação

Passos exatos, com comandos. Ações destrutivas são executadas por um humano.

## Verificação

Como saber que voltou ao normal.

## Pós-incidente

Registrar em `00-Inbox/AAAA-MM-DD-incidente-<slug>.md`; causa de design ⇒ spec ou ADR.
