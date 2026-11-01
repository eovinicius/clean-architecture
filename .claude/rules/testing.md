---
paths:
  - "app/tests/**"
  - "app/src/**"
---
# Testes

Stack real: **xUnit 2.9 + Shouldly + NSubstitute**; NetArchTest para arquitetura; Testcontainers.PostgreSql + `Microsoft.AspNetCore.Mvc.Testing` para integração.

| Projeto | O que cobre | Infra |
| --- | --- | --- |
| `app/tests/Application.UnitTests` | Handlers (happy path + **cada** `Result.Failure`), validators (`TestValidate` por regra), decorators | nenhuma |
| `app/tests/ArchitectureTests` | Fronteiras de camada e convenções | nenhuma |
| `app/tests/IntegrationTests` | HTTP real via `IntegrationTestWebAppFactory` + `BaseIntegrationTest`, Postgres em container | **Docker** |

- Nome: `Metodo_Should_Resultado_WhenCondicao` (ex.: `Handle_Should_ReturnNotFound_WhenTodoDoesNotExist`). Arrange/Act/Assert.
- Imite a classe de teste vizinha mais parecida (helpers, doubles, asserções). Ex.: `Application.UnitTests/Todos/`.
- Verifique estado persistido e efeitos colaterais (eventos de domínio, invalidação de cache), não só o retorno.
- **Entidade**: `{Entity}Tests` em `Application.UnitTests/{Feature}/` (modelo: `Application.UnitTests/Todos/TodoItemTests.cs`) cobre cada método de comportamento — sucesso, cada `Result.Failure` e o evento levantado (ou a ausência dele).
- **Seed** pela factory e pelos métodos da entidade (`TodoItem.Create(...)`, depois `.Complete(...)`/`.Archive(...)` para chegar ao estado), seguido de `ClearDomainEvents()` para o teste só enxergar os eventos do cenário. Nunca reflection nem setter aberto para montar estado; id gerado é capturado da entidade (`user.Id`), não fixado.
- Testes isolados: dados únicos por teste, sem dependência de ordem.
- `IntegrationTests/Api/OpenApiDocumentTests.cs` deve continuar verde — é o guarda do contrato HTTP.
- **Bug**: primeiro um teste que reproduz (vermelho), depois a correção.
- **Rastreabilidade SDD**: quando houver spec, comente o critério coberto — `[Fact] // SPEC-0001 AC-2`. Cobertura de uma spec = `grep -rn "SPEC-0001" app/tests`.
- Comandos: `dotnet test app/tests/Application.UnitTests` · `dotnet test app/tests/ArchitectureTests` · `dotnet test CleanArchitecture.slnx` (precisa Docker). Sem Docker? Diga no relatório em vez de declarar sucesso.
