---
title: UserId vindo do payload em Todos/Create e Todos/Copy
tags: [inbox, security, achado]
---

# UserId vindo do payload em Todos/Create e Todos/Copy

Achado da auditoria do harness em 2026-10-17. **Não corrigido**: está fora do escopo daquele PR.

- `app/src/Web.Api/Endpoints/Todos/Create.cs` recebe `UserId` no corpo do request e o repassa ao `CreateTodoCommand`. `CreateTodoCommandHandler` compara esse valor com `IUserContext.UserId` e responde 403 `Users.Unauthorized` quando são diferentes. `CopyTodoCommandHandler` segue o mesmo padrão.
- Isso contradiz `.claude/rules/security.md` ("identidade sempre do token, nunca do payload"). As rules e skills agora marcam esses slices como legado que não deve ser replicado.
- Correção sugerida: remover `UserId` do request e do command e usar `IUserContext.UserId` no handler. É mudança **breaking** no contrato HTTP de `POST /todos`, então exige decisão explícita (e spec, pela `specs.md`).
