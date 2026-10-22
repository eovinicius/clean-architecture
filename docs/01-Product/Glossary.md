---
title: Glossário
status: Draft
tags: [product, glossary]
---

# Glossário (linguagem ubíqua)

Termos de negócio do projeto. Specs, código e conversas usam **estes** nomes — sem sinônimos.
Termo novo entra aqui no mesmo PR da spec que o introduz.

> [!warning] Domínio ainda não definido
> O domínio de negócio do projeto ainda não foi definido. Por enquanto só existem os termos dos exemplos do template (`Todos`, `Users`). Não invente termos nem regras: registre a dúvida em `00-Inbox/`.

| Termo (pt-BR) | Nome no código | Definição | Status |
| --- | --- | --- | --- |
| Usuário | `User` | Conta autenticada no sistema. | vigente |
| Usuário atual | `GetCurrentUserQuery` | O Usuário identificado pelo token de acesso da requisição (`IUserContext.UserId`), nunca por id informado no request ([[SPEC-0002-consulta-do-usuario-atual]]). | vigente |
| Permissão | `Permission` | Autorização granular `recurso:ação` (ex.: `users:access`). | vigente |
| Tarefa | `TodoItem` | Item de exemplo do template, pertencente a um usuário (`Domain/Todos/TodoItem.cs`). | vigente (exemplo) |
| Prioridade | `Priority` | Prioridade de uma tarefa (`Domain/Todos/Priority.cs`). | vigente (exemplo) |
| Tarefa concluída | `TodoItem.IsCompleted` | Tarefa marcada como feita; o instante fica em `CompletedAt`. O oposto é "não concluída". | vigente (exemplo) |
| Tarefa arquivada | `TodoItem.IsArchived` | Tarefa concluída que o usuário tirou da listagem padrão sem excluí-la; o instante fica em `ArchivedAt`. É somente leitura até ser desarquivada ([[SPEC-0001-arquivamento-de-tarefas]]). | vigente (exemplo) |
| Arquivar | `ArchiveTodoCommand` | Marcar uma tarefa concluída como arquivada. Repetir a ação não altera nada ([[SPEC-0001-arquivamento-de-tarefas]]). | vigente (exemplo) |
| Desarquivar | `UnarchiveTodoCommand` | Devolver uma tarefa arquivada à listagem padrão; ela continua concluída ([[SPEC-0001-arquivamento-de-tarefas]]). | vigente (exemplo) |
