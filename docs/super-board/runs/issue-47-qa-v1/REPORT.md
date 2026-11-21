# QA v1 — issue #47 · PR #53

- Branch: `loop/issue-47` · Builder head: `f3cbb43`
- Escopo: refactor sem UI; nenhum screenshot (todos os ACs são de código, testes e docs).
- Docker indisponível na máquina do Tester: `IntegrationTests` não rodou local. Evidência do CI: run `44868866724` no head `f3cbb43`, IntegrationTests 171/171.

## Verificação local

| Comando | Resultado | Log |
|---|---|---|
| `dotnet build CleanArchitecture.slnx` | 0 avisos, 0 erros | `build.log` |
| `dotnet format CleanArchitecture.slnx --verify-no-changes --no-restore` | exit 0 | `format.log` |
| `dotnet test app/tests/Application.UnitTests --no-build` | 169/169 | `unit-tests.log` |
| `dotnet test app/tests/ArchitectureTests --no-build` | 20/20 | `architecture-tests.log` |

## Ledger AC → teste

| AC | Unit | Arquitetura | Integração (CI) |
|---|---|---|---|
| 1. Handler `internal sealed`, registrado por Scrutor | `UserRegisteredDomainEventHandlerTests.AddApplication_Should_RegisterHandlerByScan` (novo, QA) | `ConventionTests.Handlers_Should_BeInternalAndSealed` agora cobre `IDomainEventHandler<>` (QA) | `EmailConfirmationTests.Register_Should_SendExactlyOneEmailWithTokenOnlyInBody` |
| 2. Usuário registrado ⇒ `UserToken` de `EmailConfirmation` + `IEmailSender` com o e-mail | `UserRegisteredDomainEventHandlerTests.Handle_Should_PersistOnlyTokenHashAndSendTokenByEmail_WhenUserExists` | n/a | `EmailConfirmationTests.Register_Should_PersistOnlyTheTokenHash` |
| 3. `UserId` inexistente ⇒ sem e-mail, sem exceção | `UserRegisteredDomainEventHandlerTests.Handle_Should_DoNothing_WhenUserDoesNotExist` | n/a | n/a |
| 4. `RegisterUserCommandHandler` sem `IEmailSender`/`ITokenProvider`; testes sem e-mail | `RegisterUserCommandHandlerTests` (construtor de 4 deps; `UserTokens` vazio) + grep sem ocorrências | n/a | n/a |
| 5. `UserRegisteredDomainEvent` só com `UserId` | `UserTests.Create_Should_InitializeUserAndRaiseRegisteredEvent` + compilação (record de 1 parâmetro) | n/a | n/a |
| 6. `Overview.md` cita o handler | n/a: doc — `docs/03-Architecture/Overview.md:27` | n/a | n/a |
| 7. build + Unit + Arquitetura verdes; `EmailConfirmationTests` sem alteração, verde no CI | logs acima | logs acima | diff vazio em `app/tests/IntegrationTests`; CI 171/171 |

## Mutações (vistas vermelhas, revertidas, nunca commitadas)

- `UserRegisteredDomainEventHandler` trocado para `public` ⇒ `Handlers_Should_BeInternalAndSealed` falha.
- Scan Scrutor de `IDomainEventHandler<>` trocado por outro tipo ⇒ `AddApplication_Should_RegisterHandlerByScan` falha ("should have single item but had" 0).

## Test gaps (not written)

- Medium · falha da reação: `IEmailSender.SendAsync` lança depois do commit ⇒ usuário segue gravado e o cadastro responde erro (comportamento anterior, ordem em `ApplicationDbContext.SaveChangesAsync`). Sem teste; exige `IntegrationTests` com sender que falha (Docker).
- Low · ordem token-antes-do-e-mail no handler: um mutante que envia antes de gravar sobrevive aos testes unitários.
- Low · evento despachado duas vezes gera dois tokens (sem idempotência); fora do escopo do card.
