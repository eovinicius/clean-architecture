---
id: SPEC-0002
title: Consulta do usuário atual
status: Implemented
tags: [spec, users]
adrs: []
created: 2026-10-22
updated: 2026-10-22
---

# SPEC-0002 — Consulta do usuário atual

## 1. Problema

Hoje, para ler os próprios dados, o cliente chama `GET /users/{userId}` e precisa mandar o próprio id na rota. O handler (`Application/Users/GetById/GetUserByIdQueryHandler.cs`) só aceita o id que já está no token; qualquer outro responde 403 `Users.Unauthorized`. Ou seja:

- o id na rota não acrescenta nada, porque o único valor aceito já vem do token;
- a resposta de `POST /users/login` traz só `accessToken` e `refreshToken` (`Application/Users/AccessTokensResponse.cs`), então o cliente precisa decodificar o JWT para descobrir o próprio id antes dessa chamada;
- o slice recebe a identidade pelo request, o que contradiz `.claude/rules/security.md` (identidade sempre de `IUserContext.UserId`) e por isso está marcado como legado.

Esta spec cria uma rota que devolve os dados do Usuário atual direto do token, no padrão de propriedade do recurso do repositório.

**Métrica de sucesso**: um cliente que acabou de fazer login obtém os próprios dados com uma única chamada que só usa o token de acesso (AC-1), sem conhecer o próprio id.

**Critério de entrega**: todos os ACs cobertos por testes automatizados verdes, com `dotnet build` limpo e `ArchitectureTests` verde.

## 2. Escopo

- **Inclui**:
  - Rota nova `GET /users/me`, que retorna id, e-mail, nome e sobrenome do Usuário atual.
  - Slice novo na Application (`Users/GetCurrent/`), com query sem parâmetros e DTO de resposta próprio (`CurrentUserResponse`). O DTO não reaproveita o `UserResponse` de `Users/GetById/` para não acoplar o slice novo ao slice legado.
- **Não inclui**:
  - Remover, depreciar ou alterar `GET /users/{userId}`. A rota legada continua como está (decisão do usuário em 2026-10-22).
  - Editar os dados do Usuário (nome, e-mail, senha).
  - Devolver permissões, papéis ou outros campos além dos que `GET /users/{userId}` já devolve.
  - Cache da resposta.
  - Corrigir o `UserId` vindo do request em `Todos/Create`, `Todos/Copy` e `Todos/Get` (legado registrado em `docs/00-Inbox/2026-10-17-identidade-no-payload-todos.md`).

## 3. Atores e termos

- **Ator**: Usuário atual (ver [[Glossary]]).
- **Termos do [[Glossary]]**: Usuário (`User`), Permissão (`Permission`).
- **Termo novo** (adicionado ao glossário neste PR): **Usuário atual** (`GetCurrentUserQuery`), o Usuário identificado pelo token de acesso da requisição.
- "Token de acesso" é o JWT emitido por `POST /users/login`. É termo técnico de autenticação, não de negócio, e por isso não entra no glossário.

## 4. Regras de negócio

- **RN-1**: A identidade do Usuário atual vem só de `IUserContext.UserId`. A rota não declara parâmetro de id; um `userId` enviado na query string é ignorado e não altera o resultado.
- **RN-2**: A resposta traz exatamente os campos `id`, `email`, `firstName` e `lastName` do Usuário atual. Nenhum outro dado (por exemplo, hash de senha) é devolvido.
- **RN-3**: Se o Usuário do token não existe mais na base, a consulta falha com `Users.NotFound`.
- **RN-4**: Basta estar autenticado. A rota não exige permissão adicional (decisão do usuário em 2026-10-22). É diferente de `GET /users/{userId}`, que exige a Permissão `users:access`.

## 5. Critérios de aceite

Cada AC indica o nível de teste: **(unit)** = teste do handler em `Application.UnitTests`, com `IUserContext` substituído; **(integração)** = teste HTTP em `IntegrationTests`.

### Consulta

- **AC-1** — Dado um Usuário atual registrado:
  - **(integração)** quando ele chama `GET /users/me` com o próprio token de acesso, então recebe HTTP 200 com `id`, `email`, `firstName` e `lastName` iguais aos do registro.
  - **(unit)** quando o handler roda com `IUserContext.UserId` igual ao id desse Usuário, então retorna sucesso com esses mesmos campos.
- **AC-2** — **(integração)** Dado dois Usuários registrados, A e B, quando A chama `GET /users/me` com o próprio token, então a resposta traz os dados de A, nunca os de B.
- **AC-3** — **(integração)** Dado dois Usuários registrados, A e B, quando A chama `GET /users/me?userId={id de B}` com o próprio token, então recebe HTTP 200 com os dados de A (RN-1).

### Usuário inexistente

- **AC-4** — **(unit)** Dado um `IUserContext.UserId` sem Usuário correspondente na base, quando o handler roda, então falha com `Users.NotFound`.
- **AC-5** — **(integração)** Dado um token de acesso válido de um Usuário removido da base depois do login, quando ele chama `GET /users/me`, então recebe HTTP 404 com `ProblemDetails` de `title` `Users.NotFound`. O teste remove o registro pelo `ApplicationDbContext` obtido de um escopo do `IntegrationTestWebAppFactory`.

### Autorização

- **AC-6** — **(integração)** Dado um request sem header `Authorization`, quando chama `GET /users/me`, então recebe HTTP 401.
- **AC-7** — **(integração)** Dado um request com token de acesso malformado, quando chama `GET /users/me`, então recebe HTTP 401.

### Coexistência com a rota legada

- **AC-8** — **(integração)** Dado um Usuário atual autenticado, quando ele chama `GET /users/{seu id}` e depois `GET /users/me` com o mesmo token, então as duas chamadas retornam HTTP 200 com o mesmo `id`. Prova que a rota nova não captura a legada nem é capturada por ela. O teste existente `UsersTests.GetUserById_Should_ReturnUser_WhenAuthenticated` continua sendo a regressão da rota legada.

### Contrato OpenAPI

- **AC-9** — **(integração)** Dado o documento OpenAPI gerado, quando o teste `Operation_Should_RequireBearerOnlyWhenProtected` de `OpenApiDocumentTests` lê a operação `get` de `/users/me`, então ela é protegida (tem `security` e documenta a resposta 401).

## 6. Contrato HTTP

| Operação | Método e rota | Request | Response | Erros |
| --- | --- | --- | --- | --- |
| Obter Usuário atual | `GET /users/me` | — (identidade só pelo token) | 200 `CurrentUserResponse` `{ id, email, firstName, lastName }` | 401 · 404 `Users.NotFound` |

- O endpoint declara `.RequireAuthorization()`, `.WithTags(Tags.Users)`, `.WithSummary("Get the current user")`, `.Produces<CurrentUserResponse>()` e `.ProducesProblem(StatusCodes.Status404NotFound)`.
- A rota legada usa `users/{userId:guid}`. A restrição `:guid` impede que o literal `me` caia nela (AC-8).

**Mudança em endpoint existente é breaking?** Não. A rota é nova e `GET /users/{userId}` não muda. `OpenApiDocumentTests` ganha o caso `("/users/me", "get", true)` no `[Theory]` de proteção (AC-9).

## 7. Requisitos não funcionais

- **Autorização**: `.RequireAuthorization()`, sem Permissão nova (RN-4). Como a rota não é anônima, não entra na lista de exceções de `IntegrationTests/Api/EndpointAuthorizationTests.cs`.
- **Validação**: não há entrada externa além do token, então não há validator.
- **Persistência**: nenhuma mudança de schema nem migration. A leitura projeta `context.Users` direto para o DTO, filtrando por `IUserContext.UserId`.
- **Cache**: nenhum. A consulta é uma busca por chave primária.
- **Observabilidade**: nenhum sinal `app.*` novo. Os spans e logs atuais dos handlers cobrem o caso de uso.
- **Dados pessoais (LGPD)**: a resposta contém e-mail, nome e sobrenome, que são dados pessoais. Eles vão só para o próprio Usuário atual. Esses campos ficam fora de logs, traces e mensagens de erro; a mensagem de `Users.NotFound` cita só o id.
- **Volume**: chamada típica logo após o login; sem requisito de desempenho específico.

## 8. Perguntas em aberto

- [x] O que fazer com `GET /users/{userId}`? → Manter sem alteração; remoção ou depreciação ficam para outra spec. *Decidido pelo usuário em 2026-10-22.*
- [x] Qual autorização usar? → `.RequireAuthorization()`, sem Permissão `users:access` (RN-4). *Decidido pelo usuário em 2026-10-22.*
- [x] Confirmar o termo **Usuário atual** no glossário → Confirmado. *Decidido pelo usuário em 2026-10-22.*
- [ ] **Limitação conhecida** (não bloqueia): RN-4 não tem teste do tipo "Usuário sem `users:access` recebe 200 em `/users/me`". Hoje o `PermissionAuthorizationHandler` aprova toda Permissão (TODO do template), então o cenário não é montável. O teste entra quando o `PermissionProvider` for implementado. Nesse momento, o teste do AC-8 precisa conceder `users:access` ao Usuário, porque depende de `GET /users/{userId}` responder 200.

## 9. Rastreabilidade

Aprovada pelo usuário em 2026-10-22 (no chat da sessão de implementação).

Testes: `grep -rn "SPEC-0002" app/tests`.
