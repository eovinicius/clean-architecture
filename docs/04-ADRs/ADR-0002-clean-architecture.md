---
id: ADR-0002
title: Clean Architecture, CQRS sem MediatR e Result
status: Accepted
date: 2026-10-17
tags: [adr, architecture]
---

# ADR-0002 — Clean Architecture, CQRS sem MediatR e Result

Documenta retroativamente as decisões herdadas do template base.

## Contexto

A aplicação precisa de regras de negócio testáveis isoladamente, de fronteiras claras para que humanos e agentes não acoplem camadas e de tratamento de erro previsível na API.

## Decisão

- Camadas `SharedKernel ← Domain ← Application ← Infrastructure ← Web.Api`, verificadas por `app/tests/ArchitectureTests`.
- Casos de uso em vertical slices (`Application/{Feature}/{UseCase}/`) com `ICommand`/`IQuery` próprios, handlers registrados por Scrutor e decorators de validação/logging — **sem MediatR**.
- Falhas esperadas como valores (`Result`/`Error`, códigos `"{FeaturePlural}.{Reason}"`) traduzidos para `ProblemDetails` na borda.
- Acesso a dados via `IApplicationDbContext` (EF Core), sem repositórios genéricos.

## Alternativas

- **MediatR**: indireção extra para o que decorators + DI já resolvem, e licença comercial nas versões recentes. Rejeitada.
- **Exceções para fluxo de negócio**: custo, fluxo oculto e mapeamento HTTP frágil. Rejeitada.
- **Camadas horizontais (Services/Repositories)**: espalha cada caso de uso por muitas pastas. Rejeitada.

## Consequências

- Positivas: slices independentes, fáceis de gerar por skill (`add-feature`) e de revisar.
- Negativas: alguma duplicação proposital de DTOs entre slices.

## Como verificar

`app/tests/ArchitectureTests` (camadas e convenções); skill `ca-review` e subagente `architecture-reviewer`.
