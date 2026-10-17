---
name: investigate-issue
description: Investiga um erro, lentidão ou comportamento inesperado da API reproduzindo localmente e correlacionando logs, traces e métricas no Grafana LGTM (Loki, Tempo, Prometheus) com os runbooks do vault, até chegar a uma causa provável com evidência. Use quando o usuário relatar erro 500, falha intermitente, lentidão, teste de integração instável ou um comportamento que não sabe explicar.
argument-hint: <sintoma, endpoint ou mensagem de erro>
---

# investigate-issue

Objetivo: **causa provável com evidência**, não um palpite. Não corrija nada antes de confirmar a causa; a correção começa por um teste vermelho (`.claude/rules/30-testing.md`).

1. **Sintoma preciso**: endpoint, payload (sem dados pessoais reais), resposta (`ProblemDetails`: `type`, `title`, `status`), quando começou e se é intermitente. Há runbook? `grep -ril "<sintoma>" docs/06-Runbooks`. Se houver, siga-o.
2. **Reproduza localmente**:
   - `docker compose ps` — Postgres e `observability` de pé? Se não: `docker compose up -d`.
   - Rode a API (`dotnet run --project app/src/Web.Api`) e dispare a requisição, ou rode o teste que falha (`dotnet test app/tests/IntegrationTests --filter <Nome>`).
3. **Evidências no LGTM** (http://localhost:3000 → Explore; ou MCP `grafana`, se habilitado):
   - **Loki**: logs da janela do erro, filtrando por `service_name` e nível `Error`/`Warning`. Pegue o `TraceId`.
   - **Tempo**: o trace desse id — qual span falhou ou demorou (handler, query Npgsql, HTTP externo).
   - **Prometheus**: taxa de erro/latência do endpoint (`http_server_request_duration_seconds`), se for desempenho.
   - Sem Grafana: `docker compose logs web-api --since 15m` e a saída do console do Serilog.
4. **Banco** (se a suspeita for query): reveja o SQL do span do Npgsql; com o MCP `postgres-local` habilitado, rode o `EXPLAIN` contra o banco **local**.
5. **Hipóteses** ordenadas por evidência; descarte explicitamente as refutadas e diga como as refutou.
6. **Registre** em `docs/00-Inbox/AAAA-MM-DD-investigacao-<slug>.md`: sintoma, passos de reprodução, evidências (consultas usadas), causa provável e próximos passos. Causa de design ⇒ sugerir spec ou ADR; procedimento recorrente ⇒ sugerir runbook (`docs/99-Templates/TPL-Runbook.md`).
7. Proponha a correção (teste vermelho → fix) e aguarde o usuário decidir se segue.
