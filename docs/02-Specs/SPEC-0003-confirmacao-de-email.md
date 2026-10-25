---
id: SPEC-0003
title: Confirmação de e-mail
status: Implemented
tags: [spec, users]
adrs: [ADR-0004]
created: 2026-10-25
updated: 2026-10-25
---

# SPEC-0003 — Confirmação de e-mail

## 1. Problema

Hoje `POST /users/register` aceita qualquer endereço e o Usuário já consegue fazer login em seguida (`Application/Users/Register/`, `Application/Users/Login/`). Nada comprova que quem se cadastrou é dono do e-mail informado. Consequências:

- alguém pode cadastrar o e-mail de outra pessoa e usar a conta em nome dela;
- a recuperação de senha ([[SPEC-0004-recuperacao-de-senha]]) depende de o e-mail cadastrado ser de fato do Usuário.

Esta spec faz o cadastro enviar um Token de confirmação por e-mail e só libera o login depois que o Usuário confirma o endereço (decisão do usuário em 2026-10-25).

**Métrica de sucesso**: em 100% das tentativas de login com senha correta de um Usuário com e-mail não confirmado, a resposta é 403, e com e-mail confirmado é 200. A medição é feita pelos testes de AC-8 a AC-10.

**Critério de entrega**: todos os ACs cobertos por testes automatizados verdes, com `dotnet build` limpo e `ArchitectureTests` verde.

## 2. Escopo

- **Inclui**:
  - Registro do instante da confirmação no Usuário (`EmailConfirmedAt`).
  - Envio do Token de confirmação por e-mail ao cadastrar (via `IEmailSender`).
  - Rota nova `POST /users/confirm-email` (anônima), que confirma o e-mail a partir do token.
  - Rota nova `POST /users/resend-email-confirmation` (anônima), que emite um token novo.
  - Bloqueio do login de Usuário com e-mail não confirmado.
  - Tabela de tokens de uso único, compartilhada com a [[SPEC-0004-recuperacao-de-senha]] (seção 7).
  - Migration com as colunas novas, a tabela de tokens e o tratamento dos Usuários já cadastrados (RN-14, RN-15).
  - Remoção periódica, em segundo plano, de Usuários que não confirmaram o e-mail em 7 dias (RN-15 a RN-18).
- **Não inclui**:
  - Troca de e-mail do Usuário (não existe hoje).
  - Provedor real de e-mail. `LoggingEmailSender` continua sendo a implementação (Q-6).
  - Página ou link de front-end para confirmar: o e-mail leva só o token (Q-5).
  - E-mail de boas-vindas: é substituído pelo e-mail de confirmação (Q-7).
  - Aviso por e-mail antes da remoção da conta não confirmada (RN-18).
  - Mudar o dispatch de eventos de domínio (seção 7, "E-mail").
  - Contador `app.users.email_confirmations` (Q-9).
  - Bloquear `POST /users/refresh-token` por falta de confirmação (RN-12).
  - Recuperação de senha ([[SPEC-0004-recuperacao-de-senha]]).

## 3. Atores e termos

- **Atores**: visitante anônimo (cadastro, confirmação, reenvio) e Usuário (login).
- **Termos do [[Glossary]]**: Usuário (`User`).
- **Termos novos** (adicionados ao glossário neste PR, marcados "a confirmar"):
  - **E-mail confirmado** (`User.EmailConfirmedAt`): o Usuário comprovou ser dono do e-mail cadastrado; o instante fica em `EmailConfirmedAt`. O oposto é "e-mail não confirmado".
  - **Confirmar e-mail** (`ConfirmEmailCommand`): usar um Token de confirmação válido para marcar o e-mail do Usuário como confirmado.
  - **Reenviar confirmação** (`ResendEmailConfirmationCommand`): pedir um Token de confirmação novo para um e-mail.
- **Token de confirmação**: valor aleatório e opaco, enviado só por e-mail, que prova o acesso à caixa postal. É termo técnico de segurança, como o token de acesso da [[SPEC-0002-consulta-do-usuario-atual]], e por isso não entra no glossário.

## 4. Regras de negócio

**Token de confirmação**

- **RN-1**: O token tem 32 bytes de um gerador criptograficamente seguro, codificados em base64url sem padding (43 caracteres). Persiste-se só o hash SHA-256 (o mesmo esquema de `ITokenProvider.HashRefreshToken`); o valor puro existe apenas no corpo do e-mail.
- **RN-2**: O valor puro nunca aparece em resposta HTTP, log, trace, mensagem de erro nem no **assunto** do e-mail. O assunto é fixo, porque `LoggingEmailSender` registra o assunto.
- **RN-3**: O token expira 24 horas após a emissão. Ele está expirado quando `agora > emitidoEm + 24h`; no instante exato do limite, ainda vale.
- **RN-4**: Cada token só pode ser usado uma vez. Com duas confirmações simultâneas do mesmo token, exatamente uma tem sucesso.
- **RN-5**: Emitir um token novo **não** invalida os anteriores ainda válidos. Usar qualquer token de confirmação do Usuário invalida todos os outros de confirmação dele. Assim, um terceiro que pede reenvios não consegue anular o token que o dono já recebeu.
- **RN-6**: Um token de confirmação novo só é emitido se o último token de confirmação desse Usuário tiver sido emitido há mais de 60 segundos. Dentro desse intervalo, o pedido responde igual e não envia e-mail.

**Cadastro**

- **RN-7**: Todo Usuário cadastrado nasce com e-mail não confirmado (`EmailConfirmedAt` nulo).
- **RN-8**: O cadastro emite um Token de confirmação e o envia ao e-mail cadastrado. A resposta de `POST /users/register` não muda: 201 com o id.

**Confirmação**

- **RN-9**: Confirmar e-mail com um token válido grava `EmailConfirmedAt` com o instante atual (`IDateTimeProvider`) e marca o token como usado.
- **RN-10**: Se o e-mail do Usuário já está confirmado, um token válido ainda é aceito: a resposta é a mesma do sucesso, `EmailConfirmedAt` mantém o instante original e o token é marcado como usado.
- **RN-11**: Token inexistente, expirado, já usado ou invalidado faz a confirmação falhar com `Users.InvalidEmailConfirmationToken`.
- **RN-11a**: Nos quatro casos da RN-11, status, `title` e `detail` da resposta são idênticos, para não revelar qual deles ocorreu.

**Login**

- **RN-12**: O login de um Usuário com e-mail não confirmado falha com `Users.EmailNotConfirmed`, sem emitir token de acesso nem refresh token. A verificação acontece **depois** da verificação de senha: credenciais erradas continuam respondendo `Users.InvalidCredentials`, então o erro novo só aparece para quem já sabe a senha. `POST /users/refresh-token` não verifica a confirmação.

**Reenvio**

- **RN-13**: Reenviar confirmação sempre responde igual (HTTP 202, sem corpo): exista ou não um Usuário com aquele e-mail, esteja ele confirmado ou não, e esteja ou não dentro do intervalo da RN-6. Só há envio quando o e-mail pertence a um Usuário com e-mail não confirmado e fora do intervalo. O e-mail é normalizado como no cadastro (`EmailNormalizer`).
- **RN-13a** (limite aceito): o 409 `Users.EmailNotUnique` de `POST /users/register` já revela se um e-mail está cadastrado. A RN-13 só evita que o reenvio revele **mais** que isso, como o estado de confirmação. Ela não torna o sistema imune à enumeração de e-mails.

**Usuários já cadastrados**

- **RN-14**: A migration marca como confirmados todos os Usuários que já existem, com `EmailConfirmedAt` igual a `now()` do Postgres no momento em que a migration é aplicada. Usuários criados depois da migration nascem não confirmados (RN-7).

**Expiração do cadastro não confirmado**

- **RN-15**: O Usuário passa a guardar o instante do cadastro (`RegisteredAt`, via `IDateTimeProvider`). Na migration, os Usuários existentes recebem `now()`; como já ficam confirmados (RN-14), esse valor não tem efeito sobre eles.
- **RN-16**: Um Usuário com e-mail não confirmado é removido quando `agora > RegisteredAt + 7 dias`, junto com os tokens dele. A partir daí, o e-mail fica livre para um novo cadastro. Usuário com e-mail confirmado nunca é removido por esta regra.
- **RN-17**: Enquanto o Usuário não confirmado não é removido, um novo cadastro com o mesmo e-mail continua falhando com `Users.EmailNotUnique` (409), como hoje. O dono real do e-mail pode, sem esperar a remoção, pedir o reenvio, confirmar e redefinir a senha ([[SPEC-0004-recuperacao-de-senha]]).
- **RN-18**: A remoção não envia e-mail e não depende de requisição do Usuário: roda periodicamente em segundo plano.

## 5. Critérios de aceite

Cada AC indica o nível de teste: **(unit)** = entidade ou handler em `Application.UnitTests`; **(integração)** = teste HTTP em `IntegrationTests`, com `IEmailSender` substituído pelo capturador de teste descrito na seção 7. Quando um AC verifica estado persistido, o teste lê pelo `ApplicationDbContext` de um escopo do `IntegrationTestWebAppFactory`, como no AC-5 da [[SPEC-0002-consulta-do-usuario-atual]].

**Cadastro e token**

- **AC-1** — Dado um cadastro bem-sucedido:
  - **(unit)** quando `User.Create` roda, então `EmailConfirmedAt` é nulo (RN-7);
  - **(integração)** quando o visitante chama `POST /users/register`, então recebe HTTP 201 com o id e o capturador registra exatamente um e-mail para o endereço cadastrado, com um token de 43 caracteres base64url no corpo e sem o token no assunto (RN-1, RN-2, RN-8).
- **AC-2** — **(integração)** Dado um cadastro bem-sucedido, quando a tabela de tokens é lida, então o valor recebido no e-mail não aparece em nenhuma coluna, e o hash SHA-256 dele aparece (RN-1).

**Confirmação**

- **AC-3** — **(integração)** Dado o token recebido no cadastro, quando o visitante chama `POST /users/confirm-email` com ele, então recebe HTTP 204 e o Usuário passa a ter `EmailConfirmedAt` preenchido (RN-9).
- **AC-4** — Dado um token inválido, quando o visitante chama `POST /users/confirm-email`, então recebe HTTP 400 com `ProblemDetails` de `title` `Users.InvalidEmailConfirmationToken` e o mesmo `detail` em todos os casos (RN-11, RN-11a):
  - **(integração)** token que nunca foi emitido;
  - **(integração)** token já usado numa confirmação anterior;
  - **(integração)** token emitido antes de outro token de confirmação do mesmo Usuário ser usado (RN-5);
  - **(unit)** token expirado: com `IDateTimeProvider` em `emitidoEm + 24h + 1 tick`, a confirmação falha. Com `emitidoEm + 24h` exato, ela tem sucesso (RN-3).
- **AC-5** — **(integração)** Dado dois tokens válidos do mesmo Usuário, emitidos no cadastro e num reenvio, quando o segundo é usado e depois o primeiro, então o segundo responde 204 e o primeiro 400 `Users.InvalidEmailConfirmationToken`. Antes do uso, os dois eram válidos (RN-5).
- **AC-6** — **(integração)** Dado um token válido, quando duas chamadas `POST /users/confirm-email` com ele são feitas em paralelo, então exatamente uma responde 204 e a outra responde 400 `Users.InvalidEmailConfirmationToken` (RN-4).
- **AC-7** — Dado um Usuário com e-mail já confirmado e um token de confirmação dele ainda válido (preparado pelo `ApplicationDbContext`):
  - **(unit)** quando `User.ConfirmEmail` roda de novo, então `EmailConfirmedAt` não muda e nenhum evento novo é levantado;
  - **(integração)** quando o visitante chama `POST /users/confirm-email` com esse token, então recebe 204 e `EmailConfirmedAt` continua com o instante original (RN-10).
- **AC-8a** — **(integração)** Dado um body sem token, com token vazio ou com token de mais de 128 caracteres, quando o visitante chama `POST /users/confirm-email`, então recebe HTTP 400 de validação.

**Login**

- **AC-8** — **(integração)** Dado um Usuário cadastrado que ainda não confirmou o e-mail, quando ele chama `POST /users/login` com a senha correta, então recebe HTTP 403 com `title` `Users.EmailNotConfirmed` e nenhum refresh token desse Usuário é gravado (RN-12).
- **AC-9** — **(integração)** Dado um Usuário que confirmou o e-mail, quando ele chama `POST /users/login` com a senha correta, então recebe HTTP 200 com token de acesso e refresh token.
- **AC-10** — **(integração)** Dado um Usuário com e-mail não confirmado, quando o login usa a senha errada, então recebe HTTP 401 `Users.InvalidCredentials`, não `Users.EmailNotConfirmed` (RN-12).

**Reenvio**

- **AC-11** — **(integração)** Dado um Usuário com e-mail não confirmado cujo último token foi emitido há mais de 60 segundos, quando o visitante chama `POST /users/resend-email-confirmation` com o e-mail dele (com caixa e espaços diferentes do cadastro), então recebe HTTP 202 e o capturador registra um e-mail novo com um token que confirma o e-mail (RN-13). O "há mais de 60 segundos" é preparado recuando o instante de emissão do token pelo `ApplicationDbContext`.
- **AC-12** — **(integração)** Dado um Usuário com e-mail não confirmado cujo último token foi emitido há menos de 60 segundos (logo após o cadastro), quando o visitante pede o reenvio, então recebe 202 e nenhum e-mail novo é registrado (RN-6).
- **AC-13** — **(integração)** Dado um e-mail sem Usuário e, em outro caso, o e-mail de um Usuário já confirmado, quando o visitante pede o reenvio, então recebe HTTP 202 com status e corpo iguais aos do AC-11 e nenhum e-mail é registrado (RN-13).
- **AC-14** — **(integração)** Dado um body sem e-mail, com e-mail malformado ou com mais de 320 caracteres, quando o visitante pede o reenvio, então recebe HTTP 400 de validação.

**Usuários já cadastrados (migration)**

- **AC-15** — **(integração)** Dado um banco num container dedicado (fora do `IntegrationTestWebAppFactory` compartilhado), migrado com `IMigrator.MigrateAsync` até a migration imediatamente anterior a `Add_Email_Confirmation`, e um Usuário inserido por SQL:
  - quando a migration `Add_Email_Confirmation` é aplicada, então esse Usuário tem `email_confirmed_at` não nulo;
  - e um Usuário inserido depois tem `email_confirmed_at` nulo (RN-14);
  - e o Usuário antigo tem `registered_at` não nulo (RN-15).

**Autorização e contrato**

- **AC-16** — **(integração)** Dado `EndpointAuthorizationTests`, quando ele varre os endpoints, então `users/confirm-email` e `users/resend-email-confirmation` estão em `AnonymousRoutes` e o teste continua verde. Os ACs 3 e 11 provam o comportamento: as chamadas são feitas sem header `Authorization` e não recebem 401.
- **AC-17** — **(integração)** Dado o documento OpenAPI gerado, quando `OpenApiDocumentTests` roda:
  - `Operation_Should_RequireBearerOnlyWhenProtected` ganha os casos `("/users/confirm-email", "post", false)` e `("/users/resend-email-confirmation", "post", false)`: sem `security` e sem resposta 401;
  - `RequestBody_Should_MatchEndpointRequest` ganha os casos com os campos `token` e `email`, respectivamente;
  - a operação `post` de `/users/confirm-email` documenta 204 e 400; a de `/users/resend-email-confirmation` documenta 202 e 400;
  - a operação `post` de `/users/login` passa a documentar 403.

**Expiração do cadastro não confirmado**

- **AC-18** — **(unit)** Dado um Usuário com e-mail não confirmado e `RegisteredAt` = T, quando o critério de expiração (`User.IsUnconfirmedRegistrationExpired`) é avaliado em `T + 7 dias + 1 tick`, então o Usuário é selecionado para remoção; em `T + 7 dias` exato, não é (RN-16). A remoção em si, com os tokens, é provada no AC-20: o banco InMemory dos testes unitários não executa `ExecuteDeleteAsync`.
- **AC-19** — **(unit)** Dado um Usuário com e-mail **confirmado** e `RegisteredAt` de mais de 7 dias atrás, quando o critério de expiração é avaliado, então o Usuário não é selecionado (RN-16). Na integração (AC-20), um Usuário confirmado antigo também sobrevive à remoção.
- **AC-20** — **(integração)** Dado um Usuário não confirmado com `RegisteredAt` recuado 8 dias pelo `ApplicationDbContext`, quando o caso de uso de remoção é executado (resolvido do container da factory, sem esperar o agendamento) e um visitante cadastra o mesmo e-mail, então o cadastro responde 201. Antes da remoção, o mesmo cadastro responde 409 `Users.EmailNotUnique`. Os tokens do Usuário removido somem de `user_tokens`, e um Usuário confirmado com `RegisteredAt` igualmente antigo continua existindo (RN-16, RN-17).
- **AC-21** — **(integração)** Dado um Usuário não confirmado removido pela expiração, quando o visitante chama `POST /users/confirm-email` com o token que ele tinha recebido, então recebe 400 `Users.InvalidEmailConfirmationToken` (RN-11, RN-16).
- **AC-22** — **(integração)** Dado o host da API montado pelo `IntegrationTestWebAppFactory`, quando os `IHostedService` registrados são listados, então o `BackgroundService` de expiração está entre eles (RN-18). O serviço só agenda: a regra está no caso de uso do AC-20 (skill `add-background-job`).

## 6. Contrato HTTP

| Operação | Método e rota | Request | Response | Erros |
| --- | --- | --- | --- | --- |
| Confirmar e-mail | `POST /users/confirm-email` | `{ token }` | 204 | 400 validação · 400 `Users.InvalidEmailConfirmationToken` |
| Reenviar confirmação | `POST /users/resend-email-confirmation` | `{ email }` | 202 (sem corpo) | 400 validação |
| Login (alterado) | `POST /users/login` | sem mudança | sem mudança | **novo**: 403 `Users.EmailNotConfirmed` |
| Cadastro (alterado) | `POST /users/register` | sem mudança | sem mudança | sem mudança; passa a enviar o e-mail de confirmação |

- O token vai no corpo do POST, nunca na query string, para não aparecer em logs de URL.
- As duas rotas novas são anônimas e usam `.RequireRateLimiting(RateLimitingPolicies.Authentication)`, como login e cadastro. O 429 já é documentado pelo transformer de OpenAPI e coberto pelo teste existente da política, então não entra na tabela nem ganha AC próprio.
- Os endpoints declaram `.WithTags(Tags.Users)`, `.WithSummary(...)`, `.Produces(...)` com o status de sucesso e `.ProducesProblem(StatusCodes.Status400BadRequest)`. `Login.cs` ganha `.ProducesProblem(StatusCodes.Status403Forbidden)`.
- Códigos de erro novos em `UserErrors`: `Users.InvalidEmailConfirmationToken` (`ErrorType.Problem`, 400, mesmo padrão de `Users.InvalidRefreshToken`) e `Users.EmailNotConfirmed` (`ErrorType.Forbidden`, 403).

**Mudança em endpoint existente é breaking?** **Sim**, em `POST /users/login`: um Usuário com e-mail não confirmado, que hoje recebe 200, passa a receber 403. Clientes precisam tratar o 403 e oferecer o reenvio. Pela RN-14, os Usuários já cadastrados não são afetados. `POST /users/register` não muda de contrato.

## 7. Requisitos não funcionais

- **Domínio** (ADR-0004, hoje `Proposed`; a spec segue o padrão já usado em `TodoItem`):
  - `User.EmailConfirmedAt` (`DateTime?`, `private set`) e `User.RegisteredAt` (`DateTime`, `private set`). `User.Create` passa a receber o instante do cadastro, como `TodoItem.Create` recebe `createdAt`.
  - `User.ConfirmEmail(DateTime utcNow)`: na primeira vez grava o instante e levanta `UserEmailConfirmedDomainEvent(UserId)`. Se já estiver confirmado, não muda nada e não levanta evento (RN-10).
  - Entidade nova `UserToken` (`sealed`, sem navegações) com `UserId`, `Purpose` (`EmailConfirmation` | `PasswordReset`, enum), `TokenHash`, `CreatedAtUtc`, `ExpiresOnUtc` e `UsedAtUtc`. Expõe:
    - factory `Issue(...)`;
    - `IsExpired(utcNow)`, verdadeiro quando `utcNow > ExpiresOnUtc` (RN-3);
    - `IsUsable(utcNow)`;
    - `MarkUsed(utcNow)`.
  - `UserToken` não levanta eventos de domínio.
  - **O valor puro do token não entra em evento de domínio**: se um dia houver outbox, o evento seria persistido junto com o segredo. Quem emite o token (handler do cadastro ou do reenvio) envia o e-mail por `IEmailSender` depois do `SaveChangesAsync`, com o valor puro só em memória. `UserRegisteredDomainEventHandler` deixa de enviar o e-mail de boas-vindas (Q-7); se o handler ficar sem uso, é removido.
  - Os handlers só orquestram.
- **Persistência**:
  - Colunas `email_confirmed_at` (timestamp nulo) e `registered_at` (timestamp não nulo) em `users`.
  - Tabela `user_tokens` (snake_case, FK `user_id`, índice único em `token_hash`, índice em `(user_id, purpose)`), compartilhada com a [[SPEC-0004-recuperacao-de-senha]].
  - O uso único (RN-4) usa concorrência otimista na linha do token: o segundo `SaveChanges` falha com `DbUpdateConcurrencyException` e vira `Users.InvalidEmailConfirmationToken`, como já faz `RefreshTokenCommandHandler`.
  - Migration `Add_Email_Confirmation` gerada por `dotnet ef`. O backfill das RN-14 e RN-15 é um `migrationBuilder.Sql("UPDATE users SET email_confirmed_at = now(), registered_at = now()")` acrescentado à mão na migration gerada, antes do merge. Depois do merge, a migration não muda.
  - A FK de `user_tokens.user_id` remove em cascata, para que a remoção da RN-16 leve os tokens junto.
- **Remoção em segundo plano** (RN-16, RN-18):
  - Caso de uso na Application (`Users/DeleteExpiredUnconfirmed/`), sem entrada externa, que remove numa única instrução (`ExecuteDeleteAsync`) os Usuários com `EmailConfirmedAt` nulo e `RegisteredAt` vencido. O critério é avaliado no próprio `DELETE`, então uma confirmação que chega ao mesmo tempo não é apagada.
  - Ele é acionado por um `BackgroundService` periódico (skill `add-background-job`), a cada 1 hora. O intervalo é escolha técnica e fica em código.
  - Um Usuário não confirmado nunca fez login (RN-12), então não tem refresh tokens nem Tarefas a remover.
  - O job registra um log `Information` com a quantidade removida, sem e-mail nem id.
- **Limpeza**: ao emitir um token para um Usuário, os tokens dele da mesma finalidade que estejam expirados ou usados são removidos, como o login já faz com refresh tokens expirados.
- **Validação**: validators FluentValidation com limite de tamanho. O token é `NotEmpty` e `MaximumLength(128)`; o e-mail segue a regra do cadastro (`NotEmpty`, `MaximumLength(320)`, `EmailAddress`).
- **Tempo**: emissão, expiração, intervalo de reenvio e confirmação usam `IDateTimeProvider`.
- **E-mail e caminho crítico** (limite conhecido, aceito na Q-8):
  - O envio acontece dentro do request, depois do commit, como o dispatch atual de eventos (`ApplicationDbContext.SaveChangesAsync`).
  - Se `IEmailSender` falhar no cadastro, o Usuário já está gravado e o cliente recebe 500. Ele recupera pelo reenvio.
  - No reenvio, o ramo "não envia" é mais rápido que o ramo "envia". Com o `LoggingEmailSender` a diferença é desprezível; com um provedor real, ela revelaria o estado da conta pelo tempo.
  - Mudar isso (fila, outbox) é decisão estrutural, fora desta spec.
- **Abuso**: o rate limit atual é por IP. A RN-6 limita o envio por Usuário e a RN-5 impede que reenvios anulem o token legítimo.
- **Cache**: nenhum.
- **Observabilidade**: nenhum sinal `app.*` novo (Q-9).
- **Dados pessoais (LGPD)**:
  - E-mail, nome e sobrenome ficam fora de logs, traces e mensagens de erro, e o token é segredo (RN-2).
  - **Risco já existente, fora do escopo**: `LoggingEmailSender` registra o destinatário (`Email to {Recipient}`). Esta spec aumenta o número de envios, então o risco cresce. A correção fica registrada em `docs/00-Inbox/`.
- **Testes de integração**:
  - A factory registra um `IEmailSender` de teste **singleton** via `ConfigureTestServices`. Ele captura destinatário, assunto e corpo, e permite filtrar por destinatário, porque os testes compartilham a coleção.
  - `BaseIntegrationTest.RegisterAndLoginAsync` passa a confirmar o e-mail entre o cadastro e o login, usando o token capturado e `POST /users/confirm-email`. Assim, os cerca de 55 usos atuais continuam testando o mesmo comportamento.
  - Testes de `UsersTests` que cadastram e fazem login sem passar pelo helper são revistos um a um.
  - Nenhum teste existente é removido nem tem asserção afrouxada.

## 8. Perguntas em aberto

Todas decididas.

- [x] **Q-4** — Cadastro com um e-mail que já existe e não está confirmado? → Continua 409 `Users.EmailNotUnique` (RN-17), e Usuários que não confirmam em 7 dias são removidos, liberando o e-mail (RN-15, RN-16, RN-18). *Decidido pelo usuário em 2026-10-25.*
- [x] **Q-1** — O que fazer com os Usuários já cadastrados? → Marcar todos como confirmados na migration (RN-14). *Decidido pelo usuário em 2026-10-25.*
- [x] **Q-2** — Validade do Token de confirmação? → 24 horas (RN-3), fixa no código como constante. Não vai para `appsettings`, porque não é configuração de ambiente. *Validade decidida pelo usuário em 2026-10-25; a constante é escolha técnica.*
- [x] **Q-3** — Intervalo mínimo entre envios para o mesmo Usuário? → 60 segundos (RN-6), também na SPEC-0004. *Proposta aceita pelo usuário em 2026-10-25.*
- [x] **Q-7** — E-mail de boas-vindas? → Substituído pelo e-mail de confirmação: o cadastro manda um único e-mail. *Decidido pelo usuário em 2026-10-25.*
- [x] **Q-5** — Token puro ou link no e-mail? → Só o token nesta spec; o link entra quando houver front-end. *Proposta aceita pelo usuário em 2026-10-25.*
- [x] **Q-6** — Como obter o token em desenvolvimento? → Fica para outra spec (SMTP de desenvolvimento pediria ADR). Até lá, o token só é obtido pelos testes de integração. *Proposta aceita pelo usuário em 2026-10-25.*
- [x] **Q-8** — Envio dentro do request? → Aceito como limite conhecido até haver fila ou outbox (seção 7). *Proposta aceita pelo usuário em 2026-10-25.*
- [x] **Q-9** — Contador `app.users.email_confirmations`? → Não nesta spec. *Proposta aceita pelo usuário em 2026-10-25.*

## 9. Rastreabilidade

Aprovada pelo usuário em 2026-10-25 (no chat: "manda bala"). Na implementação, os AC-18, AC-19 e AC-22 mudaram de nível de teste, como descrito em cada um.

Relacionada: [[SPEC-0004-recuperacao-de-senha]] (depende do estado "e-mail confirmado" e compartilha a tabela `user_tokens`).

Testes: `grep -rn "SPEC-0003" app/tests`.
