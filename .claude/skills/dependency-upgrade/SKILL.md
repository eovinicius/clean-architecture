---
name: dependency-upgrade
description: Atualiza pacotes NuGet com segurança via Central Package Management — inventário de desatualizados e vulneráveis, versões reais pelo MCP nuget, leitura de breaking changes, edição de Directory.Packages.props e validação com build e testes. Use para upgrades de pacotes, patches de segurança, PRs do Dependabot com falha ou quando o usuário pedir para atualizar dependências.
argument-hint: '[pacote ou família, ex.: "EF Core" — vazio = inventário completo]'
---

# dependency-upgrade

O Dependabot (`.github/dependabot.yml`) já abre PRs diários agrupados. Esta skill cuida do que ele não resolve sozinho: majors, breaking changes, PRs com build quebrado e correções de vulnerabilidade.

1. **Inventário**:
   - `dotnet list CleanArchitecture.slnx package --outdated`
   - `dotnet list CleanArchitecture.slnx package --vulnerable --include-transitive`
2. **Versão real** de cada candidato via MCP `nuget` (ou nuget.org). Nunca chute versões. Release notes e breaking changes via `context7`/`microsoft-learn`.
3. **Classifique**: patch (aplicar) · minor (aplicar + ler changelog) · major (plano para o usuário + possível ADR). Pacotes de IA/LLM são proibidos (ADR-0003).
4. **Atualize famílias juntas**, sempre em `Directory.Packages.props` (nunca `Version` no `.csproj`):
   - EF Core: `Microsoft.EntityFrameworkCore*` + `Npgsql.EntityFrameworkCore.PostgreSQL` + `EFCore.NamingConventions` (mesmo major).
   - ASP.NET Core 10: `Microsoft.AspNetCore.*` + `Microsoft.Extensions.*` alinhados.
   - OpenTelemetry: `OpenTelemetry.*` + `Npgsql.OpenTelemetry`.
   - Testes: `xunit*`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`.
5. **Valide**: `dotnet restore` → `dotnet build CleanArchitecture.slnx` (o SonarAnalyzer novo pode trazer warnings = erros; corrija ou justifique) → `dotnet test CleanArchitecture.slnx` (integração precisa de Docker).
6. **Commit** (só se pedido), um por família: `chore(deps): atualiza OpenTelemetry para 1.x.y`.
7. Major, mudança de licença ou troca de biblioteca ⇒ registre no PR e, se estrutural, faça um ADR (skill `create-adr`).
