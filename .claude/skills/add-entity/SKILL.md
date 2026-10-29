---
name: add-entity
description: Add a new domain entity to the Clean Architecture template — entity class, error catalog, domain events, EF Core configuration, DbContext wiring, and migration. Use when the user asks to add an entity, aggregate, domain model, or table.
argument-hint: <entity description, e.g. "Project with a name, owner, and list of todos">
---

# Add a Domain Entity

Create a new entity and wire it through every layer, following the `TodoItem` pattern.

## Files to create/modify

1. **Entity** — `app/src/Domain/{Feature}/{Entity}.cs`

```csharp
using SharedKernel;

namespace Domain.Projects;

public sealed class Project : Entity
{
    private Project()
    {
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Project Create(Guid ownerId, string name, DateTime createdAt)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = name,
            CreatedAt = createdAt
        };

        project.Raise(new ProjectCreatedDomainEvent(project.Id));

        return project;
    }

    public void Rename(string name)
    {
        Name = name;

        Raise(new ProjectRenamedDomainEvent(Id));
    }
}
```

**Rich model, never anemic** (`.claude/rules/architecture.md`, Domain — ADR-0004): `sealed class` inheriting `Entity` (gives it `DomainEvents` + `Raise(...)`); every property `{ get; private set; }` and a private parameterless constructor for EF Core; a static `Create(...)` factory that generates the `Id`, copies incoming collections (`[.. labels]`) and raises the `Created` event; one behavior method per state change, returning `Result` with an `{Entity}Errors` error when the rule can fail (`void` when it can't). Non-deterministic values (`DateTime createdAt`) come in as parameters. Collections initialized with `= [];`. Live model: `app/src/Domain/Todos/TodoItem.cs`. `ConventionTests` fails the build on a public setter or an unsealed class.

2. **Error catalog** — `app/src/Domain/{Feature}/{Entity}Errors.cs`

```csharp
using SharedKernel;

namespace Domain.Projects;

public static class ProjectErrors
{
    public static Error NotFound(Guid projectId) => Error.NotFound(
        "Projects.NotFound",
        $"The project with the Id = '{projectId}' was not found");
}
```

Codes are `"{EntityPlural}.{Reason}"` — the plural of the entity, not of the feature folder (`TodoItem` in `Todos/` → `"TodoItems.NotFound"`). Pick the factory by semantics: `Error.NotFound` (404), `Error.Conflict` (409), `Error.Problem` (400), `Error.Unauthorized` (401), `Error.Forbidden` (403), `Error.Failure` (500).

3. **Domain events** — one record per file, `app/src/Domain/{Feature}/{Entity}{PastTenseVerb}DomainEvent.cs`

```csharp
using SharedKernel;

namespace Domain.Projects;

public sealed record ProjectCreatedDomainEvent(Guid ProjectId) : IDomainEvent;
```

Create at minimum the `Created` event; add others as commands need them. Events carry ids, not entities.

4. **EF configuration** — `app/src/Infrastructure/{Feature}/{Entity}Configuration.cs`

```csharp
using Domain.Projects;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Projects;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasOne<User>().WithMany().HasForeignKey(p => p.OwnerId);
    }
}
```

Relationships are configured shadow-style (`HasOne<User>().WithMany()`) — entities hold foreign-key ids, not navigation properties. Configurations are picked up automatically by `ApplyConfigurationsFromAssembly`.

5. **DbContext wiring** — add `DbSet<{Entity}> {Plural}` to **both**:
   - `app/src/Application/Abstractions/Data/IApplicationDbContext.cs`
   - `app/src/Infrastructure/Database/ApplicationDbContext.cs`

   Also add the `DbSet` to `app/tests/Application.UnitTests/Abstractions/TestDbContext.cs` so handlers stay unit-testable.

6. **Entity tests** — `app/tests/Application.UnitTests/{Feature}/{Entity}Tests.cs` (model: `Application.UnitTests/Todos/TodoItemTests.cs`): `Create` and every behavior method — success, each `Result.Failure` and the event raised (or not).

7. **Migration** — from the repo root:

```
dotnet ef migrations add Add_{Plural} --project app/src/Infrastructure --startup-project app/src/Web.Api
```

Migration names are `PascalCase_With_Underscores` (see `Add_RefreshTokens`).

## Rules

- The Domain project references only `SharedKernel` — no EF, no Application types. Persistence concerns (keys, conversions, relationships) live exclusively in the Infrastructure configuration.
- EF Core maps the private setters and constructor with no extra configuration — never open a setter for EF (`.claude/rules/persistence.md`).
- Run `dotnet build` and `dotnet test` when done — `ArchitectureTests` enforce the layer rules, private setters and `sealed`.
- If the user also wants use cases for the entity, continue with the `add-feature` skill.
