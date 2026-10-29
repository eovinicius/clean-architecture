---
paths:
  - "app/src/Infrastructure/**"
  - "app/src/Domain/**"
---
# Persistência (EF Core 10 + PostgreSQL)

Skills: `add-entity`, `add-migration`.

- `ApplicationDbContext` (`Infrastructure/Database`) com naming **snake_case** (EFCore.NamingConventions) e schemas em `Database/Schemas.cs`.
- Configurações `IEntityTypeConfiguration<T>` em `Infrastructure/{Feature}/` são aplicadas automaticamente. Chaves, conversões, índices e relacionamentos ficam lá.
- Entidades usam FK ids (relacionamentos shadow), **sem navegações**.
- Entidades ricas (`architecture.md`, Domain): o EF materializa pelo construtor privado e grava pelos `private set`, sem configuração extra. Mapeamento especial (backing field, conversão) vai na configuração — **nunca** abra setter público para o EF. Após mudar entidade, `dotnet ef migrations has-pending-model-changes --project app/src/Infrastructure --startup-project app/src/Web.Api` diz se precisa de migration.
- Novo filtro/ordenação em lista ⇒ avalie índice na configuração.

## Migrations

- Geradas **somente** por `dotnet ef migrations add <Pascal_With_Underscores> --project app/src/Infrastructure --startup-project app/src/Web.Api` (ex.: `Add_Todo_Archived_At`). Nunca escreva/edite `*.Designer.cs` ou `ApplicationDbContextModelSnapshot.cs` à mão.
- **Imutáveis após merge** em `development`/`main` (o hook `protect-files.sh` bloqueia). Correção = nova migration.
- Revise o arquivo gerado antes de aceitar:
  - drop/rename de coluna ou tabela ⇒ **pare** e proponha expand/contract (adicionar novo → migrar dados → remover antigo em migration posterior);
  - `NOT NULL` em tabela existente ⇒ default ou backfill;
  - índice em tabela grande ⇒ avaliar `CREATE INDEX CONCURRENTLY` via `migrationBuilder.Sql(..., suppressTransaction: true)`.
- `dotnet ef database update` só contra o Postgres **local** do compose (hook bloqueia hosts remotos). Em Development a API também aplica migrations no startup (`Web.Api/Extensions/MigrationExtensions.cs`).
