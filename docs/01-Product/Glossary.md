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
| Permissão | `Permission` | Autorização granular `recurso:ação` (ex.: `users:access`). | vigente |
| Tarefa | `TodoItem` | Item de exemplo do template, pertencente a um usuário (`Domain/Todos/TodoItem.cs`). | vigente (exemplo) |
| Prioridade | `Priority` | Prioridade de uma tarefa (`Domain/Todos/Priority.cs`). | vigente (exemplo) |
