---
id: RB-001
title: Ambiente local (API, Postgres, observabilidade e testes)
status: Approved
tags: [runbook, local]
---

# RB-001 — Ambiente local

## Subir

```bash
docker compose up -d                       # postgres (5432) + observability/LGTM (3000, 4317, 4318)
dotnet run --project app/src/Web.Api       # em Development aplica migrations no startup
```

Ou suba tudo, inclusive a API em container: `docker compose up -d --build` (API em http://localhost:5000).

- Scalar/OpenAPI: disponível em Development (ver `Program.cs`).
- Grafana: http://localhost:3000 → Explore → Loki (logs), Tempo (traces), Prometheus (métricas).

## Migrations

```bash
dotnet ef migrations add <Pascal_With_Underscores> --project app/src/Infrastructure --startup-project app/src/Web.Api
dotnet ef database update --project app/src/Infrastructure --startup-project app/src/Web.Api   # só contra o Postgres local
```

`dotnet ef` ausente → `dotnet tool install --global dotnet-ef`.

## Testes

| Sintoma | Causa provável | Ação |
| --- | --- | --- |
| `IntegrationTests` falham ao iniciar container | Docker Desktop parado | Iniciar o Docker; `docker info` deve responder |
| Porta 5432 ocupada | Postgres local fora do compose | Parar o serviço local ou mudar a porta no compose |
| Build falha com warning | `TreatWarningsAsErrors` sempre ligado | Corrigir o warning — não suprimir sem justificativa |
| CI falha em "Verify formatting" | Diferença de formatação | `dotnet format CleanArchitecture.slnx` e commitar |

## Resetar o banco local

Os dados ficam em `./.containers/db` (bind mount). Para zerar: `docker compose down`, apagar `./.containers/db` **manualmente** (o harness bloqueia o agente de apagá-lo) e subir de novo.
