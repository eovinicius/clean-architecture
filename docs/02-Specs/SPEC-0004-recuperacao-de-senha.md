---
id: SPEC-0004
title: Recuperação de senha
status: Implemented
tags: [spec, users]
adrs: [ADR-0004]
created: 2026-10-25
updated: 2026-10-25
---

# SPEC-0004 — Recuperação de senha

## 1. Problema

Hoje um Usuário que esquece a senha não tem como voltar a entrar na conta. A API não tem nenhuma rota para trocar ou redefinir a senha (`Application/Users/` só tem `Register`, `Login`, `Refresh`, `GetById` e `GetCurrent`), e `User.PasswordHash` só é gravado no cadastro.

Esta spec cria a recuperação de senha por e-mail: o Usuário pede a redefinição, recebe um Token de redefinição no e-mail cadastrado e o usa para escolher uma senha nova.

**Métrica de sucesso**: um Usuário que esqueceu a senha volta a fazer login com uma senha nova usando só o próprio e-mail (AC-1 + AC-5), sem ajuda de suporte.

**Critério de entrega**: todos os ACs cobertos por testes automatizados verdes, com `dotnet build` limpo e `ArchitectureTests` verde.

## 2. Escopo

- **Inclui**:
  - Rota nova `POST /users/forgot-password` (anônima), que envia o Token de redefinição por e-mail.
  - Rota nova `POST /users/reset-password` (anônima), que troca a senha a partir do token.
  - Revogação das sessões (refresh tokens) do Usuário depois da redefinição (RN-11).
  - Uso da tabela `user_tokens` criada pela [[SPEC-0003-confirmacao-de-email]], com finalidade `PasswordReset`.
- **Não inclui**:
  - Trocar a senha estando autenticado (senha atual + nova). É outra feature.
  - E-mail de aviso de "senha alterada" (Q-5).
  - Invalidar tokens de acesso (JWT) já emitidos (RN-11).
  - Mudar a regra de senha do cadastro (Q-3).
  - Fazer a redefinição confirmar o e-mail (Q-1).
  - Provedor real de e-mail e link de front-end (Q-5 e Q-6 da [[SPEC-0003-confirmacao-de-email]]).

## 3. Atores e termos

- **Atores**: visitante anônimo (pede a redefinição e redefine a senha).
- **Termos do [[Glossary]]**: Usuário (`User`), E-mail confirmado (`User.EmailConfirmedAt`, de [[SPEC-0003-confirmacao-de-email]]).
- **Termos novos** (adicionados ao glossário neste PR, marcados "a confirmar"):
  - **Solicitar redefinição de senha** (`RequestPasswordResetCommand`): pedir que um Token de redefinição seja enviado ao e-mail informado.
  - **Redefinir senha** (`ResetPasswordCommand`): trocar a senha do Usuário usando um Token de redefinição válido.
- **Termos técnicos** (não entram no glossário, como o token de acesso da [[SPEC-0002-consulta-do-usuario-atual]]):
  - **Token de redefinição**: valor aleatório e opaco, enviado só por e-mail, que autoriza uma única troca de senha.
  - **Sessão**: uma família de refresh tokens (`RefreshToken.FamilyId`), aberta por um login e mantida pelas rotações de `POST /users/refresh-token`.

## 4. Regras de negócio

**Token de redefinição** (mesmo formato e armazenamento do Token de confirmação da [[SPEC-0003-confirmacao-de-email]])

- **RN-1**: O token tem 32 bytes de um gerador criptograficamente seguro, codificados em base64url sem padding (43 caracteres). Persiste-se só o hash SHA-256; o valor puro existe apenas no corpo do e-mail.
- **RN-2**: O valor puro nunca aparece em resposta HTTP, log, trace, mensagem de erro nem no assunto do e-mail.
- **RN-3**: O token expira 1 hora após a emissão. Ele está expirado quando `agora > emitidoEm + 1h`; no instante exato do limite, ainda vale.
- **RN-4**: Cada token só pode ser usado uma vez. Com duas redefinições simultâneas do mesmo token, exatamente uma tem sucesso.
- **RN-5**: Emitir um token novo não invalida os anteriores ainda válidos. Usar qualquer Token de redefinição do Usuário invalida todos os outros de redefinição dele.
- **RN-6**: Um Token de redefinição novo só é emitido se o último Token de redefinição desse Usuário tiver sido emitido há mais de 60 segundos (mesma decisão da Q-3 da [[SPEC-0003-confirmacao-de-email]]). Dentro desse intervalo, o pedido responde igual e não envia e-mail.

**Solicitação**

- **RN-7**: Solicitar redefinição de senha sempre responde igual (HTTP 202, sem corpo): exista ou não um Usuário com aquele e-mail, e esteja ou não dentro do intervalo da RN-6. O e-mail é normalizado como no cadastro (`EmailNormalizer`). Como na SPEC-0003 RN-13a, o 409 do cadastro continua revelando quais e-mails estão cadastrados; esta regra só evita revelar mais que isso.
- **RN-8**: Quando o e-mail pertence a um Usuário, um Token de redefinição é emitido e enviado a esse e-mail. Isso vale também para Usuário com e-mail não confirmado.

**Redefinição**

- **RN-9**: A senha nova é obrigatória, tem no mínimo 8 caracteres (a mesma regra do cadastro) e no máximo 128 caracteres.
- **RN-10**: Redefinir senha com um token válido grava o hash da senha nova (`IPasswordHasher`) e marca o token como usado.
- **RN-11**: Redefinir a senha revoga todos os refresh tokens ativos do Usuário, em todas as sessões. Os tokens de acesso (JWT) já emitidos continuam válidos até expirar, porque são stateless.
- **RN-12**: Token inexistente, expirado, já usado ou invalidado faz a redefinição falhar com `Users.InvalidPasswordResetToken`, e a senha não muda.
- **RN-12a**: Nos quatro casos da RN-12, status, `title` e `detail` da resposta são idênticos.
- **RN-13**: Redefinir a senha não altera `EmailConfirmedAt`. Um Usuário com e-mail não confirmado continua sem login até confirmar ([[SPEC-0003-confirmacao-de-email]] RN-12).

## 5. Critérios de aceite

Cada AC indica o nível de teste: **(unit)** = entidade ou handler em `Application.UnitTests`; **(integração)** = teste HTTP em `IntegrationTests`, com o `IEmailSender` capturador da [[SPEC-0003-confirmacao-de-email]]. Quando um AC verifica estado persistido, o teste lê pelo `ApplicationDbContext` de um escopo do `IntegrationTestWebAppFactory`. Os Usuários dos ACs estão com e-mail confirmado, salvo no AC-11.

**Solicitação**

- **AC-1** — **(integração)** Dado um Usuário cadastrado, quando o visitante chama `POST /users/forgot-password` com o e-mail dele (com caixa e espaços diferentes do cadastro), então recebe HTTP 202 e o capturador registra exatamente um e-mail para o endereço cadastrado, com um token de 43 caracteres base64url no corpo e sem o token no assunto (RN-1, RN-2, RN-7, RN-8).
- **AC-2** — **(integração)** Dado um e-mail sem Usuário, quando o visitante chama `POST /users/forgot-password`, então recebe HTTP 202 com status e corpo iguais aos do AC-1 e nenhum e-mail é registrado (RN-7).
- **AC-3** — **(integração)** Dado uma solicitação bem-sucedida, quando a tabela `user_tokens` é lida, então o valor recebido no e-mail não aparece em nenhuma coluna e o token tem finalidade `PasswordReset` (RN-1).
- **AC-4** — **(integração)** Dado um Usuário que acabou de solicitar a redefinição, quando ele solicita de novo em menos de 60 segundos, então recebe 202 e nenhum e-mail novo é registrado (RN-6).
- **AC-4a** — **(integração)** Dado um body sem e-mail, com e-mail malformado ou com mais de 320 caracteres, quando o visitante chama `POST /users/forgot-password`, então recebe HTTP 400 de validação.

**Redefinição**

- **AC-5** — **(integração)** Dado o Token de redefinição recebido, quando o visitante chama `POST /users/reset-password` com ele e uma senha nova válida, então recebe HTTP 204. O login com a senha nova responde 200 e o login com a senha antiga responde 401 `Users.InvalidCredentials` (RN-10).
- **AC-6** — Dado um token inválido, quando o visitante chama `POST /users/reset-password`, então recebe HTTP 400 com `ProblemDetails` de `title` `Users.InvalidPasswordResetToken`, com o mesmo `detail` em todos os casos, e o login com a senha antiga continua respondendo 200 (RN-12, RN-12a):
  - **(integração)** token que nunca foi emitido;
  - **(integração)** token já usado numa redefinição anterior;
  - **(integração)** token emitido antes de outro Token de redefinição do mesmo Usuário ser usado (RN-5). O segundo token é obtido recuando, pelo `ApplicationDbContext`, o instante de emissão do primeiro;
  - **(unit)** token expirado: com `IDateTimeProvider` em `emitidoEm + 1h + 1 tick`, a redefinição falha. Com `emitidoEm + 1h` exato, ela tem sucesso (RN-3).
- **AC-7** — **(integração)** Dado um token válido, quando duas chamadas `POST /users/reset-password` com ele e senhas diferentes são feitas em paralelo, então exatamente uma responde 204 e a outra responde 400 `Users.InvalidPasswordResetToken`. Só a senha da chamada bem-sucedida faz login (RN-4).
- **AC-8** — **(integração)** Dado um token válido, quando o visitante chama `POST /users/reset-password` sem token, com token de mais de 128 caracteres, com senha nova vazia, com 7 caracteres ou com 129 caracteres, então recebe HTTP 400 de validação. O token continua válido: uma chamada seguinte, com senha de 8 caracteres e outra de 128, responde 204 (RN-9).

**Sessões**

- **AC-9** — **(integração)** Dado um Usuário com duas sessões abertas (dois logins), quando ele redefine a senha, então `POST /users/refresh-token` com o refresh token de qualquer uma das duas responde 400 `Users.InvalidRefreshToken` (RN-11).
- **AC-10** — **(unit)** Dado um Usuário com refresh tokens ativos em duas famílias e um já revogado, quando o handler de redefinição roda com token válido, então todos os ativos ficam com `RevokedAtUtc` preenchido e o já revogado mantém o instante original (RN-11).

**E-mail não confirmado**

- **AC-11** — **(integração)** Dado um Usuário com e-mail não confirmado ([[SPEC-0003-confirmacao-de-email]]), quando ele solicita e conclui a redefinição, então recebe 202 e 204, o login com a senha nova responde 403 `Users.EmailNotConfirmed` e `EmailConfirmedAt` continua nulo (RN-8, RN-13).

**Autorização e contrato**

- **AC-12** — **(integração)** Dado `EndpointAuthorizationTests`, quando ele varre os endpoints, então `users/forgot-password` e `users/reset-password` estão em `AnonymousRoutes` e o teste continua verde. Os ACs 1 e 5 provam o comportamento: as chamadas são feitas sem header `Authorization` e não recebem 401.
- **AC-13** — **(integração)** Dado o documento OpenAPI gerado, quando `OpenApiDocumentTests` roda:
  - `Operation_Should_RequireBearerOnlyWhenProtected` ganha os casos `("/users/forgot-password", "post", false)` e `("/users/reset-password", "post", false)`: sem `security` e sem resposta 401;
  - `RequestBody_Should_MatchEndpointRequest` ganha os casos com os campos `email` e `token` + `newPassword`, respectivamente;
  - a operação `post` de `/users/forgot-password` documenta 202 e 400; a de `/users/reset-password` documenta 204 e 400.

## 6. Contrato HTTP

| Operação | Método e rota | Request | Response | Erros |
| --- | --- | --- | --- | --- |
| Solicitar redefinição de senha | `POST /users/forgot-password` | `{ email }` | 202 (sem corpo) | 400 validação |
| Redefinir senha | `POST /users/reset-password` | `{ token, newPassword }` | 204 | 400 validação · 400 `Users.InvalidPasswordResetToken` |

- O token vai no corpo do POST, nunca na query string.
- As duas rotas são anônimas e usam `.RequireRateLimiting(RateLimitingPolicies.Authentication)`. O 429 já é documentado pelo transformer de OpenAPI, então não entra na tabela.
- Os endpoints declaram `.WithTags(Tags.Users)`, `.WithSummary(...)`, `.Produces(...)` com o status de sucesso e `.ProducesProblem(StatusCodes.Status400BadRequest)`.
- Código de erro novo em `UserErrors`: `Users.InvalidPasswordResetToken` (`ErrorType.Problem`, 400, mesmo padrão de `Users.InvalidRefreshToken`).

**Mudança em endpoint existente é breaking?** Não. As duas rotas são novas e nenhum endpoint existente muda.

## 7. Requisitos não funcionais

- **Domínio** (ADR-0004, hoje `Proposed`):
  - `User.ResetPassword(string passwordHash)` troca `PasswordHash` (`private set`) e levanta `UserPasswordResetDomainEvent(UserId)`, sem e-mail nem senha no evento.
  - O token usa a entidade `UserToken` da [[SPEC-0003-confirmacao-de-email]] com `Purpose = PasswordReset`.
  - Como lá, o valor puro do token não entra em evento de domínio: o handler da solicitação envia o e-mail por `IEmailSender` depois do `SaveChangesAsync`.
  - A revogação das sessões usa `RefreshToken.Revoke(utcNow)`.
  - Os handlers só orquestram.
- **Persistência**:
  - Nenhuma tabela nova: usa `user_tokens` da [[SPEC-0003-confirmacao-de-email]]. Por isso, esta spec é implementada depois dela.
  - Se a implementação precisar de mudança de schema (ex.: valor novo no enum persistido), ela vem numa migration nova, gerada por `dotnet ef`.
  - O uso único (RN-4) usa concorrência otimista na linha do token, como na SPEC-0003.
  - A redefinição grava `users.password_hash`, marca o token como usado e revoga os refresh tokens num único `SaveChangesAsync`.
- **Limpeza**: ao emitir um Token de redefinição, os tokens expirados ou usados dessa finalidade do Usuário são removidos.
- **Validação**: validators FluentValidation com limite de tamanho. O e-mail segue a regra do cadastro; o token é `NotEmpty` e `MaximumLength(128)`; a senha nova segue a RN-9.
- **Tempo**: emissão, expiração, intervalo e revogação usam `IDateTimeProvider`.
- **E-mail e caminho crítico**: mesmo limite conhecido da [[SPEC-0003-confirmacao-de-email]] (Q-8 de lá). O envio é feito dentro do request e o ramo "e-mail não existe" é mais rápido.
- **Abuso**: rate limit por IP mais o intervalo por Usuário (RN-6). A RN-5 impede que um terceiro anule o token que o dono já recebeu.
- **Cache**: nenhum.
- **Observabilidade**: nenhum sinal `app.*` novo. Auditoria (Q-6): quando a senha é redefinida, um log estruturado `Information` com o `UserId` e a quantidade de sessões revogadas, sem e-mail, token nem senha.
- **Dados pessoais e segredos (LGPD)**:
  - E-mail fora de logs, traces e mensagens de erro.
  - O Token de redefinição e a senha nova são segredos (RN-2). Eles nunca vão para log nem trace, e a senha nova não aparece em `ProblemDetails` de validação. O `LoggingDecorator` não pode logar o command.
  - O risco já existente do `LoggingEmailSender` registrar o destinatário está descrito na SPEC-0003, seção 7.

## 8. Perguntas em aberto

Todas decididas. A remoção de Usuários não confirmados depois de 7 dias ([[SPEC-0003-confirmacao-de-email]] RN-16) também apaga os Tokens de redefinição deles, e nenhuma regra daqui muda por isso.

- [x] **Q-1** — Usuário com e-mail não confirmado pode redefinir a senha? → Pode, e a redefinição **não** confirma o e-mail (RN-8, RN-13). *Decidido pelo usuário em 2026-10-25.*
- [x] **Q-2** — Validade do Token de redefinição? → 1 hora (RN-3), fixa no código como constante. *Validade decidida pelo usuário em 2026-10-25; a constante é escolha técnica.*
- [x] **Q-3** — Tamanho máximo da senha nova? → 128 caracteres (RN-9). O cadastro continua sem limite máximo até outra spec alinhar (registrado em `docs/00-Inbox/`). *Decidido pelo usuário em 2026-10-25.*
- [x] **Q-4** — Redefinir a senha encerra todas as sessões? → Sim (RN-11). *Decidido pelo usuário em 2026-10-25.*
- [x] **Q-5** — E-mail de aviso "sua senha foi alterada"? → Não nesta spec. *Proposta aceita pelo usuário em 2026-10-25.*
- [x] **Q-6** — Log de auditoria da redefinição? → Sim: um log `Information` com o `UserId` e nenhum dado pessoal (seção 7). *Proposta aceita pelo usuário em 2026-10-25.*

## 9. Rastreabilidade

Aprovada pelo usuário em 2026-10-25 (no chat: "manda bala").

Depende de: [[SPEC-0003-confirmacao-de-email]] (estado "e-mail confirmado", bloqueio de login e tabela `user_tokens`). Implementar depois dela.

Testes: `grep -rn "SPEC-0004" app/tests`.
