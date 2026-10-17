---
paths:
  - "app/src/**"
  - "docker-compose*.yml"
---
# 50 · Observabilidade (Serilog + OpenTelemetry → Grafana LGTM)

Ambiente local: `docker compose up -d` sobe `grafana/otel-lgtm` (Grafana em http://localhost:3000; OTLP em 4317/4318). Mapa de sinais: `docs/03-Architecture/Observability.md`. Skill: `instrument-observability`.

- **Setup único**: traces/métricas em `app/src/Web.Api/Extensions/OpenTelemetryExtensions.cs`; logs via Serilog configurado em `appsettings*.json` (sink OpenTelemetry). Não crie exporters, `TracerProvider` ou sinks paralelos.
- **Traces**: ASP.NET Core, HttpClient e Npgsql já são instrumentados. Span manual só para trabalho relevante sem instrumentação; nome `<verbo> <objeto>`. Todo `ActivitySource`/`Meter` novo precisa de `.AddSource(...)`/`.AddMeter(...)` em `OpenTelemetryExtensions.cs` — sem isso o sinal não é exportado.
- **Métricas de negócio**: `IMeterFactory`; nome `app.<contexto>.<medida>` com unidade UCUM. **Cardinalidade baixa**: tags só com enums/flags — nunca ids, e-mail, texto livre ou mensagem de erro.
- **Logs**: estruturados; Information para eventos de negócio, Warning para falha esperada, Error para inesperada. O `LoggingDecorator` já registra cada command/query — não duplique.
- **Nunca logar/tagear PII** (conteúdo livre informado pelo usuário, nome, e-mail, telefone), tokens ou connection strings.
- Correlação é automática (trace context). Não crie IDs de correlação próprios.
- Sinal novo ou renomeado ⇒ atualizar `docs/03-Architecture/Observability.md` no mesmo PR. Confira no Grafana (Explore → Tempo/Loki/Prometheus).
