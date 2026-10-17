# 60 · Segurança e LGPD (sempre carregada)

- **AuthN**: JWT + refresh tokens com rotação (Infrastructure/Authentication). Não altere o fluxo de tokens sem pedido explícito.
- **AuthZ**: todo endpoint novo declara `.RequireAuthorization()` ou `.HasPermission("<recurso>:<ação>")`. Anônimo só se intencional e justificado no PR.
- **Propriedade do recurso** (padrão único): a identidade vem **sempre** de `IUserContext.UserId` (token), nunca do payload. Recurso de outro usuário ⇒ a consulta filtra por `UserId` e o handler retorna `{Entity}Errors.NotFound` (**404**, não revela que o recurso existe). Modelo: `Application/Todos/GetById/`. `Todos/Create` e `Todos/Copy` recebem `UserId` no request e respondem 403 `UserErrors.Unauthorized()` — é legado do template, **não replique**.
- **Entrada**: toda entrada externa tem validator FluentValidation com limites de tamanho; SQL só via EF/LINQ ou `FromSql` parametrizado — nunca concatenação.
- **Controles existentes** (rate limiting, security headers, CORS, idempotência, hashing de senha, `GlobalExceptionHandler`) não são afrouxados sem pedido explícito. Nunca exponha stack trace ou mensagem interna ao cliente.
- **Segredos**: `dotnet user-secrets` local (o compose monta `UserSecrets`); nada real em `appsettings*.json`, código, testes, logs, docs ou prompts. As credenciais `postgres/postgres` do compose são apenas de desenvolvimento local. Os hooks bloqueiam `.env`, `*.pfx`, `*.pem`, `secrets/`.
- **LGPD**: dados de usuários (nome, e-mail, telefone) e conteúdo livre que eles informam (ex.: descrição de tarefa) são dados pessoais. Fora de logs, traces, tags de métrica e mensagens de erro; respostas devolvem só o necessário.
- **Dependências**: pacote novo exige justificativa (e ADR se estrutural); preferir libs mantidas e licença permissiva; `dotnet list package --vulnerable` antes do PR. Pacotes de IA proibidos (00-core #2).
- Revisão dedicada: subagente `security-reviewer` quando o diff tocar auth, entrada de dados, logs, config ou dependências.
