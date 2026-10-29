---
name: instrument-observability
description: Adiciona métricas de negócio, spans e logs estruturados no padrão OpenTelemetry + Serilog do repositório (exportados para o Grafana LGTM local), com controle de cardinalidade e sem dados pessoais. Use quando uma spec pedir métricas ou SLIs, ao investigar falta de visibilidade, ou ao criar spans e logs para um fluxo novo.
argument-hint: <o que precisa ser observado, ex.: "tarefas concluídas por prioridade">
---

# instrument-observability

Regras: `.claude/rules/observability.md`. Setup existente: `app/src/Web.Api/Extensions/OpenTelemetryExtensions.cs` (traces e métricas) + Serilog em `appsettings*.json` (logs → Loki).

1. **Defina a pergunta** que o sinal responde (ex.: "quantas tarefas são concluídas por prioridade?") e a unidade UCUM (`ms`, `s`, `{todo}`).
2. **Métrica**:
   - Classe `{Feature}Metrics` na Application, em `Application/{Feature}/{Feature}Metrics.cs` (ex.: `TodoMetrics` em `Application/Todos/`), `internal sealed`, criada com `IMeterFactory`; `public const string MeterName = "App.{Feature}"`.
   - Instrumento nomeado `app.<contexto>.<medida>` (ex.: `app.todos.completed`, com a tag `priority` — enum `Domain/Todos/Priority.cs`).
   - Tags **apenas** de baixa cardinalidade (enums, bool). Nunca ids, e-mail, texto livre ou mensagem de erro.
   - Registre como singleton e exporte: `.AddMeter(TodoMetrics.MeterName)` em `WithMetrics(...)` no `OpenTelemetryExtensions.cs`.
3. **Span** (só se não houver auto-instrumentação cobrindo o trabalho): `ActivitySource` estático com o mesmo nome do Meter, `.AddSource(...)` em `WithTracing(...)`; `using Activity? activity = Source.StartActivity("verbo objeto");` em falha, `activity?.SetStatus(ActivityStatusCode.Error)`.
4. **Logs**: `ILogger<T>` com template estruturado e o nível certo. O `LoggingDecorator` já registra cada command/query — não duplique. Nada de dados pessoais.
5. **Teste** (métricas críticas): `MetricCollector<T>` (`Microsoft.Extensions.Diagnostics.Testing`) ou `MeterListener` no teste do handler. Se o pacote não existir em `Directory.Packages.props`, peça confirmação antes de adicionar.
6. **Confira localmente**: `docker compose up -d`, exercite o endpoint e veja em http://localhost:3000 → Explore (Prometheus para métricas, Tempo para traces, Loki para logs).
7. **Docs**: linha nova na tabela "Sinais de negócio" de `docs/03-Architecture/Observability.md`.
