---
paths:
  - "**/*.cs"
  - "**/*.csproj"
  - "Directory.*.props"
  - ".editorconfig"
---
# 20 · C# 14 / .NET 10

O build é o lint: `Directory.Build.props` liga **sempre** `AnalysisMode=All`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild` e SonarAnalyzer. Warning = build quebrado.

- `Nullable` habilitado: modele a nulidade, não silencie com `!`.
- Siga o `.editorconfig`: file-scoped namespaces, estilo de `var`, chaves, ordem de usings. O CI roda `dotnet format --verify-no-changes`.
- Prefira: primary constructors para DI, collection expressions (`[]`, `[.. x]`), `record` para DTOs, pattern matching/switch expressions.
- Comparações de cultura invariável (`ToUpperInvariant`, `StringComparison.Ordinal*`).
- Async de ponta a ponta: sufixo `Async`, `CancellationToken` propagado até a última chamada. Nunca `.Result`/`.Wait()`/`async void`.
- Logging: Serilog/`ILogger` com templates estruturados (`{TodoId}`), nunca interpolação; nunca PII.
- Exceções: não capture `Exception` genérico fora de fronteiras (handler global, background job).
- Visibilidade mínima: `internal sealed` por padrão; `public` só o que é contrato.
- **Central Package Management**: versão só em `Directory.Packages.props`; no `.csproj`, `<PackageReference Include="X" />` sem `Version`. Não use `dotnet add package --version`.
- Comente pouco: só o *porquê* não óbvio. Siga a densidade de comentários do arquivo.
