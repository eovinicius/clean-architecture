---
id: ADR-0004
title: Modelo de domínio rico com estado encapsulado
status: Proposed
date: 2026-10-22
tags: [adr, architecture, domain]
supersedes:
superseded_by:
---

# ADR-0004 — Modelo de domínio rico com estado encapsulado

## Contexto

O template veio com entidades anêmicas: `TodoItem`, `User` e `RefreshToken` só tinham setters públicos, e as regras de estado ficavam nos handlers da Application. Isso valia para as RN-4 e RN-5 de [[SPEC-0001-arquivamento-de-tarefas]], para a regra "só arquiva concluída" e para a emissão e revogação de refresh token. As consequências observadas:

- **Invariantes contornáveis.** Qualquer handler, teste ou job podia escrever `IsArchived = true` sem passar pela regra, e nada no build impedia.
- **Regra duplicada.** A emissão de refresh token estava copiada em Login e em Refresh. A regra "arquivada não muda" aparecia em mais de um handler.
- **Agentes replicam o que veem.** A skill `add-entity` pedia "plain settable properties", e a `add-feature` mostrava um handler mutando campos. O padrão anêmico se propagava a cada feature nova.

A refatoração da branch `feature/refatoracao-codigo-limpo` moveu as regras para as entidades. Esta ADR registra o padrão para que ele não regrida.

## Decisão

As entidades do Domain encapsulam o próprio estado, e os handlers só orquestram:

- Propriedades com `private set` e construtor privado sem parâmetros, usado só pelo EF Core. Nenhum setter público, nem `init`, em classe do Domain.
- A criação acontece por uma factory estática `{Entity}.Create(...)` (ou um verbo de negócio, como `RefreshToken.Issue`). Ela gera o `Id`, copia as coleções recebidas e levanta o evento `{Entity}CreatedDomainEvent` quando a entidade herda `Entity`.
- Cada transição de estado é um método da entidade (`Complete`, `Archive`, `UpdateDescription`...). O método valida as invariantes, retorna `Result` com o erro do catálogo `{Entity}Errors` quando a regra de negócio pode falhar (ou `void` quando não pode) e levanta o próprio evento de domínio.
- Valores não determinísticos entram como parâmetro (`DateTime utcNow`). A entidade não resolve relógio nem usuário.
- O handler busca com filtro de propriedade, chama o método, propaga a falha, salva e invalida o cache. Existência e propriedade do recurso continuam no handler, porque dependem de consulta.
- Classes e records concretos são `sealed`, exceto quando foram projetados para herança.

## Alternativas

- **Manter o modelo anêmico com regras nos handlers**: é mais simples de escrever, mas não protege as invariantes e duplica regra entre casos de uso. Foi o que causou o problema descrito no Contexto. Rejeitada.
- **Domain services para as regras, mantendo entidades anêmicas**: centraliza a regra, mas a entidade continua aberta a escrita direta, e o serviço precisa ser lembrado em todo caso de uso. Rejeitada.
- **Agregados com repositórios e unit of work próprios**: contradiz [[ADR-0002-clean-architecture]], que decidiu acesso a dados via `IApplicationDbContext` sem repositórios. É uma indireção desnecessária para o tamanho atual. Rejeitada.

## Consequências

- Positivas:
  - As invariantes ficam num lugar só e podem ser testadas sem banco (`Application.UnitTests/{Feature}/{Entity}Tests.cs`).
  - Os handlers ficam curtos e uniformes.
  - O build barra regressão.
- Negativas / riscos aceitos:
  - Os testes não montam mais o estado por inicializador de objeto. Eles usam a factory e os métodos de comportamento, e depois `ClearDomainEvents()` para isolar os eventos do cenário.
  - Uma operação idempotente que não altera nada ainda passa por `SaveChangesAsync`, sem escrita, e invalida o cache. É um custo aceito para manter a regra só na entidade.
  - O EF Core materializa as entidades pelo construtor privado. Se uma entidade nova precisar de mapeamento especial (backing field, valor complexo), ele vai para a `IEntityTypeConfiguration<>`, nunca por meio de setter público.

## Como verificar

- `app/tests/ArchitectureTests/Conventions/ConventionTests.cs`:
  - `DomainClasses_Should_NotHavePublicSetters`: nenhuma propriedade com `set` ou `init` público em classe do Domain.
  - `Classes_Should_BeSealed_WhenNotInherited`: classes e records concretos dos assemblies de produção são `sealed`, salvo quando têm subclasse. Migrations geradas ficam de fora.
- Checklist da skill `ca-review` e subagente `architecture-reviewer`.
- Modelo vivo: `app/src/Domain/Todos/TodoItem.cs`.
