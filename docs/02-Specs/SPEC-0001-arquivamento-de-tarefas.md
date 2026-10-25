---
id: SPEC-0001
title: Arquivamento de tarefas
status: Implemented
tags: [spec, todos]
adrs: []
created: 2026-10-18
updated: 2026-10-18
---

# SPEC-0001 — Arquivamento de tarefas

## 1. Problema

Tarefas concluídas ficam para sempre na listagem padrão de `GET /todos`. Com o tempo, as não concluídas se perdem entre as concluídas antigas, e hoje a única saída é excluir a tarefa, o que apaga o histórico. O usuário precisa tirar uma tarefa concluída da frente sem perdê-la.

Esta spec também é a primeira feature a passar por todas as camadas do template (domínio, migration, slice da Application, endpoint, cache e testes) e serve para validar o fluxo SDD e o harness de ponta a ponta.

**Métrica de sucesso**: depois de arquivar Tarefas, `GET /todos` sem `includeArchived` retorna apenas Tarefas não arquivadas (AC-10), e nenhuma Tarefa arquivada é perdida: todas continuam acessíveis por id e por `includeArchived=true` (AC-9, AC-11).

**Critério de entrega**: todos os ACs cobertos por testes automatizados verdes, com `dotnet build` limpo e `ArchitectureTests` verde.

## 2. Escopo

- **Inclui**:
  - Arquivar uma Tarefa concluída e desarquivar uma Tarefa arquivada.
  - Expor o estado de arquivamento (`IsArchived`, `ArchivedAt`) nas respostas de `GET /todos` e `GET /todos/{id}`.
  - Tirar as Tarefas arquivadas da listagem padrão de `GET /todos`, com o parâmetro opcional `includeArchived` para incluí-las.
  - Bloquear edição e conclusão de Tarefa arquivada.
  - Persistência (colunas novas + migration) e invalidação do cache por id.
- **Não inclui**:
  - Arquivamento automático (por tempo ou ao concluir).
  - Arquivamento ou desarquivamento em lote.
  - Listar **somente** as arquivadas (fica fora: decisão do usuário por `includeArchived`).
  - Reabrir (desconcluir) uma Tarefa.
  - Corrigir o `userId` vindo do request em `GET /todos`, `POST /todos` e `POST /todos/{todoId}/copy` (legado registrado em `docs/00-Inbox/2026-10-17-identidade-no-payload-todos.md`).
  - Métricas de negócio novas.

## 3. Atores e termos

- **Ator**: Usuário autenticado, dono da Tarefa.
- **Termos do [[Glossary]]**: Usuário (`User`), Tarefa (`TodoItem`), Tarefa concluída (`TodoItem.IsCompleted`).
- **Termos novos** (adicionados ao glossário neste PR): **Tarefa concluída** (`TodoItem.IsCompleted`, estado que já existia no template sem entrada), **Tarefa arquivada** (`TodoItem.IsArchived`), **Arquivar** (`ArchiveTodoCommand`) e **Desarquivar** (`UnarchiveTodoCommand`).

## 4. Regras de negócio

- **RN-1**: Só uma Tarefa concluída (`IsCompleted = true`) pode ser arquivada.
- **RN-2**: Arquivar marca a Tarefa como arquivada (`IsArchived = true`), registra `ArchivedAt` com o instante atual (`IDateTimeProvider.UtcNow`) e emite o evento de domínio `TodoItemArchivedDomainEvent`.
- **RN-3**: Desarquivar desmarca a Tarefa (`IsArchived = false`), limpa `ArchivedAt` (`null`) e emite o evento de domínio `TodoItemUnarchivedDomainEvent`. A Tarefa continua concluída.
- **RN-4**: Arquivar e desarquivar são idempotentes. Arquivar uma Tarefa já arquivada, ou desarquivar uma Tarefa que não está arquivada, responde sucesso sem alterar nada: `ArchivedAt` original preservado e nenhum evento de domínio emitido.
- **RN-5**: Uma Tarefa arquivada é somente leitura. Atualizar (`PUT /todos/{id}`) e concluir (`PUT /todos/{id}/complete`) falham com `TodoItems.Archived`. A verificação de arquivamento vem antes da de conclusão: concluir uma Tarefa arquivada retorna `TodoItems.Archived`, não `TodoItems.AlreadyCompleted`.
- **RN-6**: Uma Tarefa arquivada pode ser consultada por id (`GET /todos/{id}`), excluída (`DELETE /todos/{id}`) e copiada (`POST /todos/{todoId}/copy`), como qualquer outra. A cópia nasce não concluída e não arquivada.
- **RN-7**: A listagem `GET /todos` omite as Tarefas arquivadas por padrão. Com `includeArchived=true`, as arquivadas aparecem junto com as demais, sob os mesmos filtros de busca, ordenação e paginação.
- **RN-8**: O Usuário só arquiva e desarquiva as próprias Tarefas. A identidade vem do token (`IUserContext.UserId`), nunca do request.
- **RN-9**: Toda Tarefa existente antes desta feature começa como não arquivada (`IsArchived = false`, `ArchivedAt = null`).

## 5. Critérios de aceite

Cada AC indica o nível de teste: **(unit)** = teste do handler em `Application.UnitTests`, que verifica estado da entidade, `DomainEvents` e `IDateTimeProvider` substituído; **(integração)** = teste HTTP em `IntegrationTests`, observável só pela API.

Nos ACs de listagem, o `userId` da query de `GET /todos` é sempre o do próprio Usuário autenticado (exigência do legado, ver seção 6).

### Arquivar

- **AC-1** — Dado uma Tarefa concluída e não arquivada do Usuário:
  - **(integração)** quando ele chama `PUT /todos/{id}/archive`, então recebe HTTP 204 e `GET /todos/{id}` retorna `isArchived = true` com `archivedAt` preenchido.
  - **(unit)** quando o handler de arquivar roda, então a Tarefa fica com `IsArchived = true`, `ArchivedAt` igual a `IDateTimeProvider.UtcNow`, e `TodoItemArchivedDomainEvent` é emitido.
- **AC-2** — **(unit + integração)** Dado uma Tarefa não concluída (`IsCompleted = false`) do Usuário, quando ele chama `PUT /todos/{id}/archive`, então a chamada falha com `TodoItems.NotCompleted` (HTTP 400) e a Tarefa continua não arquivada.
- **AC-3** — **(unit)** Dado uma Tarefa já arquivada do Usuário, com `ArchivedAt = T1`, e um `IDateTimeProvider` que agora retorna `T2 ≠ T1`, quando o handler de arquivar roda, então retorna sucesso, `ArchivedAt` continua `T1` e nenhum evento de domínio é emitido. **(integração)** Chamar `PUT /todos/{id}/archive` duas vezes retorna HTTP 204 nas duas.

### Desarquivar

- **AC-4** — Dado uma Tarefa arquivada do Usuário:
  - **(integração)** quando ele chama `PUT /todos/{id}/unarchive`, então recebe HTTP 204 e `GET /todos/{id}` retorna `isArchived = false`, `archivedAt = null` e `isCompleted = true`.
  - **(unit)** quando o handler de desarquivar roda, então `TodoItemUnarchivedDomainEvent` é emitido.
- **AC-5** — **(unit)** Dado uma Tarefa não arquivada do Usuário, quando o handler de desarquivar roda, então retorna sucesso, a Tarefa não muda e nenhum evento de domínio é emitido. **(integração)** `PUT /todos/{id}/unarchive` numa Tarefa não arquivada retorna HTTP 204.

### Somente leitura

- **AC-6** — **(unit + integração)** Dado uma Tarefa arquivada do Usuário, quando ele chama `PUT /todos/{id}` para atualizá-la, então a chamada falha com `TodoItems.Archived` (HTTP 400) e `GET /todos/{id}` mostra a descrição original.
- **AC-7** — **(unit + integração)** Dado uma Tarefa arquivada do Usuário, quando ele chama `PUT /todos/{id}/complete`, então a chamada falha com `TodoItems.Archived` (HTTP 400), não `TodoItems.AlreadyCompleted`.
- **AC-8** — **(integração)** Dado uma Tarefa arquivada do Usuário, quando ele chama `DELETE /todos/{id}`, então recebe HTTP 204 e `GET /todos/{id}` passa a retornar `TodoItems.NotFound` (HTTP 404).

### Leitura e listagem

- **AC-9** — **(integração)** Dado uma Tarefa arquivada do Usuário, quando ele chama `GET /todos/{id}`, então recebe HTTP 200 com `isArchived = true` e `archivedAt` preenchido.
- **AC-10** — **(integração)** Dado um Usuário com Tarefas arquivadas e não arquivadas, quando ele chama `GET /todos?userId={seuId}` sem `includeArchived`, então a resposta (HTTP 200) contém só as não arquivadas e `totalCount` conta só elas.
- **AC-11** — **(integração)** Dado o mesmo cenário do AC-10, quando ele chama `GET /todos?userId={seuId}&includeArchived=true`, então a resposta (HTTP 200) contém as arquivadas e as não arquivadas, cada uma com `isArchived` correto.

### Cache

- **AC-12** — **(integração)** Dado que `GET /todos/{id}` já colocou a Tarefa em cache, quando o Usuário a arquiva (ou desarquiva) e chama `GET /todos/{id}` de novo, então a resposta reflete o novo estado (`isArchived` e `archivedAt` atualizados), sem valor velho do cache.

### Autorização

- **AC-13** — **(integração)** Dado um request sem token válido, quando chama `PUT /todos/{id}/archive` ou `PUT /todos/{id}/unarchive`, então recebe HTTP 401.
- **AC-14** — **(unit + integração)** Dado uma Tarefa de outro Usuário (ou um id inexistente), quando o Usuário chama `PUT /todos/{id}/archive` ou `PUT /todos/{id}/unarchive`, então a chamada falha com `TodoItems.NotFound` (HTTP 404), sem revelar se a Tarefa existe, e a Tarefa do outro Usuário não muda.

### Validação

- **AC-15** — **(unit do validator + integração)** Dado `id = 00000000-0000-0000-0000-000000000000` (`Guid.Empty`, que a restrição `{id:guid}` da rota aceita), quando o Usuário chama `PUT /todos/{id}/archive` ou `PUT /todos/{id}/unarchive`, então recebe HTTP 400 com `ProblemDetails` de validação.

### Cópia

- **AC-16** — **(integração)** Dado uma Tarefa arquivada do Usuário, quando ele chama `POST /todos/{todoId}/copy`, então recebe HTTP 200 com o id da cópia, e `GET /todos/{idDaCópia}` retorna `isArchived = false`, `archivedAt = null` e `isCompleted = false`.

## 6. Contrato HTTP

| Operação | Método e rota | Request | Response | Erros |
| --- | --- | --- | --- | --- |
| Arquivar Tarefa | `PUT /todos/{id:guid}/archive` | — (id na rota) | 204 | 400 `TodoItems.NotCompleted` · 400 validação · 401 · 404 `TodoItems.NotFound` |
| Desarquivar Tarefa | `PUT /todos/{id:guid}/unarchive` | — (id na rota) | 204 | 400 validação · 401 · 404 `TodoItems.NotFound` |
| Listar Tarefas (alterada) | `GET /todos` | query `userId` (existente, obrigatório, legado) + nova opcional `includeArchived` (`bool`, padrão `false`) | 200 `PagedList<TodoResponse>` com os campos novos | 400 do binding do minimal API para `includeArchived` não booleano (o endpoint já declara `.ProducesProblem(400)`) |
| Obter Tarefa (alterada) | `GET /todos/{id:guid}` | — | 200 `TodoResponse` com os campos novos | sem erros novos |
| Atualizar Tarefa (alterada) | `PUT /todos/{id:guid}` | inalterado | 204 | **novo**: 400 `TodoItems.Archived` (o endpoint já declara 400) |
| Concluir Tarefa (alterada) | `PUT /todos/{id:guid}/complete` | inalterado | 204 | **novo**: 400 `TodoItems.Archived` (o endpoint já declara 400) |

- **Endpoints novos** declaram, como `Complete.cs`: `.WithSummary(...)`, `.Produces(204)`, `.ProducesProblem(400)`, `.ProducesProblem(404)` e `.RequireAuthorization()`.
- **Campos novos** em `TodoResponse` (os dois `TodoResponse`, de `Get` e de `GetById`): `isArchived` (`bool`) e `archivedAt` (`DateTime?`, UTC).
- **`GetTodosQuery`** ganha `bool IncludeArchived`. O único uso atual é `Web.Api/Endpoints/Todos/Get.cs`, que passa a repassar o parâmetro novo.

**Mudança em endpoint existente é breaking?** Não no contrato: os campos novos de resposta e o parâmetro opcional são aditivos, e os erros novos usam o formato `ProblemDetails` que já existe. **Mas há mudança de comportamento** que o cliente percebe:

- sem `includeArchived`, `GET /todos` deixa de retornar Tarefas arquivadas;
- `PUT /todos/{id}` e `PUT /todos/{id}/complete` passam a falhar para Tarefas arquivadas.

Antes desta feature nenhuma Tarefa é arquivada (RN-9), então nenhum cliente atual é afetado até arquivar algo. O contrato é guardado por `IntegrationTests/Api/OpenApiDocumentTests.cs`, que deve ser revisado depois da mudança.

## 7. Requisitos não funcionais

- **Autorização**: as rotas novas usam `.RequireAuthorization()`, igual às demais rotas de Tarefa. Nenhuma permissão nova. A propriedade do recurso segue o padrão de `Application/Todos/GetById/`: a consulta filtra por `IUserContext.UserId` e responde 404 `TodoItems.NotFound`.
- **Validação**: `ArchiveTodoCommandValidator` e `UnarchiveTodoCommandValidator` exigem `TodoItemId` não vazio, como `CompleteTodoCommandValidator` (AC-15).
- **Persistência**:
  - Colunas novas em `todo_items`: `is_archived` (`boolean not null default false`, o que já atende a RN-9) e `archived_at` (`timestamp with time zone null`).
  - Migration gerada por `dotnet ef`; nome sugerido `Add_Todo_Archived_Fields`.
  - Nenhum índice novo: o filtro `is_archived` atua sobre o conjunto já filtrado por `user_id`, e o volume por Usuário é baixo.
- **Cache**:
  - Arquivar e desarquivar removem `TodoCacheKeys.ById(userId, todoItemId)` quando o estado muda. A listagem não é cacheada hoje e continua sem cache.
  - O `HybridCache` está registrado só em memória (`services.AddHybridCache()` em `Infrastructure/DependencyInjection.cs`, sem cache distribuído), então o deploy esvazia o cache e não sobram entradas antigas sem os campos novos.
- **Observabilidade**: nenhum sinal `app.*` novo. Os spans e logs atuais dos handlers cobrem os casos de uso novos.
- **Dados pessoais (LGPD)**: nenhum dado pessoal novo. `Description` continua fora de logs, traces e mensagens de erro; as mensagens dos erros novos citam só o id da Tarefa.
- **Volume**: o mesmo das operações atuais de Tarefa; sem requisito de desempenho específico.

## 8. Perguntas em aberto

- [x] Quais Tarefas podem ser arquivadas? → Só as concluídas (RN-1). *Decidido pelo usuário em 2026-10-18.*
- [x] Tarefa arquivada pode ser editada? → Não, é somente leitura; consultar e excluir continuam liberados (RN-5, RN-6). *Decidido pelo usuário em 2026-10-18.*
- [x] Como a listagem trata as arquivadas? → Omitidas por padrão, com `includeArchived=true` para incluir (RN-7). *Decidido pelo usuário em 2026-10-18.*
- [x] Arquivar o que já está arquivado? → Idempotente, 204 (RN-4). *Decidido pelo usuário em 2026-10-18.*
- [x] Copiar uma Tarefa arquivada é permitido? → Sim, sem mudança no slice `Copy` (RN-6, AC-16). *Decidido pelo usuário em 2026-10-18.*

## 9. Rastreabilidade

Aprovada pelo usuário em 2026-10-18 (no chat da sessão de implementação).

Testes: `grep -rn "SPEC-0001" app/tests`.
