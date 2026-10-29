---
title: Observabilidade
tags: [architecture, observability]
---

# Observabilidade

Serilog + OpenTelemetry exportando por OTLP para o container `grafana/otel-lgtm` (Grafana em http://localhost:3000). Regras: `.claude/rules/observability.md`.

| Sinal | Onde é configurado | Destino |
| --- | --- | --- |
| Traces | `app/src/Web.Api/Extensions/OpenTelemetryExtensions.cs` (ASP.NET Core, HttpClient, Npgsql) | Tempo |
| Métricas | Mesmo arquivo (ASP.NET Core, HttpClient, runtime) | Prometheus |
| Logs | Serilog em `appsettings*.json` (sink OpenTelemetry) | Loki |

O exportador OTLP de traces/métricas só é ligado quando `OTEL_EXPORTER_OTLP_ENDPOINT` está definido (o `docker-compose.override.yml` define).

## Sinais de negócio

Métricas `app.<contexto>.<medida>` e spans manuais criados pelo time. Atualize esta tabela no mesmo PR que criar ou renomear um sinal.

| Nome | Tipo | Unidade | Tags | Origem |
| --- | --- | --- | --- | --- |
| — | — | — | — | *(nenhum ainda)* |
