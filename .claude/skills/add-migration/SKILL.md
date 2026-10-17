---
name: add-migration
description: Create and apply an EF Core migration in the Clean Architecture template. Use when the user changes the model (adds or edits an entity or its configuration) and needs a migration, or asks to update the database schema.
argument-hint: <migration name or a description of the model change, e.g. "Add_Projects">
---

# Add an EF Core Migration

Migrations live in the **Infrastructure** project; **Web.Api** is the startup project (it owns the connection string and DI).

## Steps

1. Finish the model change first — entity + `IEntityTypeConfiguration` + a `DbSet` on `ApplicationDbContext` **and** `IApplicationDbContext` (see `/add-entity`). The migration reads the model, so it must compile.

2. Create the migration from the repo root:

   ```
   dotnet ef migrations add {Migration_Name} --project app/src/Infrastructure --startup-project app/src/Web.Api
   ```

   Names are `PascalCase_With_Underscores` (e.g. `Add_Projects`, `Add_RefreshTokens`).

3. Review the generated files in `app/src/Infrastructure/Database/Migrations/` — check the `Up`/`Down`, snake_case table/column names, and that nothing unexpected was dropped. Never accept it blindly:
   - **Drop or rename** of a column/table → stop and propose expand/contract to the user (add the new column + copy data now; drop the old one in a later migration).
   - **`NOT NULL` on an existing table** without a default → it fails on existing rows. Add a default or a backfill step.
   - **Index on a large table** → consider `migrationBuilder.Sql("CREATE INDEX CONCURRENTLY ...", suppressTransaction: true)`.
   - New filter/sort column in a list query → make sure the configuration declares an index.

4. Apply it. In Development the app runs `ApplyMigrations()` on startup, so `dotnet run --project app/src/Web.Api` (with `docker compose up -d` running) applies it. To apply explicitly:

   ```
   dotnet ef database update --project app/src/Infrastructure --startup-project app/src/Web.Api
   ```

## Rules

- Needs the EF tool: `dotnet tool install --global dotnet-ef` if `dotnet ef` is missing.
- Never edit a migration that has already been applied/committed — add a new one. The `protect-files.sh` hook blocks edits to migrations already on `main`/`development`, and to `ApplicationDbContextModelSnapshot.cs` (generated).
- `dotnet ef database update` only against the local compose Postgres — the `guard-shell.sh` hook blocks remote hosts.
- To drop the last **unapplied** migration: `dotnet ef migrations remove --project app/src/Infrastructure --startup-project app/src/Web.Api`.
- Commit the migration together with the model change.
