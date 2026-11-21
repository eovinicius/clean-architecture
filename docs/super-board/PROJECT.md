---
title: PROJECT (super-board)
tags: [engineering, super-board]
---
# Projeto — contexto para as lanes do super-board

- **O que é**: API sobre o template Clean Architecture em .NET 10 / C# 14, monolito modular, PostgreSQL (EF Core), Serilog + OpenTelemetry. Domínio de negócio ainda indefinido: só os slices de exemplo em `app/src/Application/`.
- **Contrato**: `AGENTS.md` (invariantes, comandos, Definition of Done) e `.claude/rules/` são a fonte de verdade — leia antes de construir. Este arquivo não repete regras.
- **Base**: `development` (Gitflow). Branches do robô: `loop/issue-N`, PR para `development`.
- **Verificação no merge gate**: `dotnet build CleanArchitecture.slnx` · `dotnet format --verify-no-changes` · `Application.UnitTests` · `ArchitectureTests`. `IntegrationTests` exigem Docker (Testcontainers) e rodam no CI.
- **Migrations**: geradas por `dotnet ef` em `app/src/Infrastructure/Database/Migrations`, aplicadas no startup da API (`app/src/Api/Extensions/MigrationExtensions.cs`); nunca editadas após merge.
- **Sempre humano**: autenticação/autorização (JWT, refresh tokens, `[AllowAnonymous]`, `[HasPermission]`), schema destrutivo, PR acima de 400 linhas.
- **Proibido**: pacote/SDK de LLM em `app/` ou `Directory.Packages.props`; PII em logs (LGPD); segredos em código/config.
