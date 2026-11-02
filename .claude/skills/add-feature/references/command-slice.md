# Command Slice Templates

Files go in `app/src/Application/{Feature}/{UseCase}/`. Replace `{Feature}` (plural feature folder), `{Entity}` (entity class), `{entity}` (camelCase variable), `{EntityPlural}` (DbSet and error-code prefix), `{Verb}`/`{Behavior}` (use-case verb and entity method), and `{Reason}` (error reason) throughout. Live model: the closest analogous command slice in `app/src/Application/`.

> Some template commands carry `UserId` in the command and answer 403 `UserErrors.Unauthorized()`. That is template legacy (`.claude/rules/security.md`) — never copy their shape.

## Command

Positional record for few parameters:

```csharp
using Application.Abstractions.Messaging;

namespace Application.{Feature}.{UseCase};

public sealed record {Verb}{Entity}Command(Guid {Entity}Id) : ICommand;
```

Command that creates a resource and returns its id. The owner is **not** a field: the handler takes it from `IUserContext` (example entity from the `add-entity` skill):

```csharp
using Application.Abstractions.Messaging;

namespace Application.Projects.Create;

public sealed record CreateProjectCommand(string Name) : ICommand<Guid>;
```

- `ICommand` → handler returns `Result` (endpoint responds `204 NoContent`).
- `ICommand<TResponse>` → handler returns `Result<TResponse>` (endpoint responds `201 Created` when it creates a resource, `200 Ok` otherwise).

## Validator

`internal sealed`, same folder. Auto-registered (`includeInternalTypes: true`) and executed by `ValidationDecorator` before the handler runs. Every string gets a maximum length. Time-dependent rules inject `IDateTimeProvider` — never `DateTime.Today`/`DateTime.UtcNow`.

```csharp
using FluentValidation;

namespace Application.Projects.Create;

internal sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
    }
}
```

## Handler

`internal sealed`, primary constructor, `IApplicationDbContext` for data access. The handler **orchestrates**: it loads the entity filtered by owner (missing → `NotFound`), calls the entity's behavior method — which enforces the rule, returns `Result` and raises the domain event — propagates a failure, saves, invalidates the cache, and returns. It never assigns properties nor calls `Raise` (`.claude/rules/architecture.md`, Domain). The rule lives in `{Entity}.{Behavior}` (`app/src/Domain/{Feature}/{Entity}.cs`):

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.{Feature};
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.{Feature}.{UseCase};

internal sealed class {Verb}{Entity}CommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IUserContext userContext,
    HybridCache cache)
    : ICommandHandler<{Verb}{Entity}Command>
{
    public async Task<Result> Handle({Verb}{Entity}Command command, CancellationToken cancellationToken)
    {
        {Entity}? {entity} = await context.{EntityPlural}
            .SingleOrDefaultAsync(e => e.Id == command.{Entity}Id && e.UserId == userContext.UserId, cancellationToken);

        if ({entity} is null)
        {
            return {Entity}Errors.NotFound(command.{Entity}Id);
        }

        Result result = {entity}.{Behavior}(dateTimeProvider.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync({Feature}CacheKeys.ById({entity}.UserId, {entity}.Id), cancellationToken);

        return Result.Success();
    }
}
```

Creating handler — the owner comes from the token:

```csharp
internal sealed class CreateProjectCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IUserContext userContext)
    : ICommandHandler<CreateProjectCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateProjectCommand command, CancellationToken cancellationToken)
    {
        var project = Project.Create(userContext.UserId, command.Name, dateTimeProvider.UtcNow);

        context.Projects.Add(project);

        await context.SaveChangesAsync(cancellationToken);

        return project.Id;
    }
}
```

Notes:

- **Ownership (single pattern)**: filter the lookup by `userContext.UserId` and return `{Entity}Errors.NotFound` (404) when nothing matches — another user's resource is indistinguishable from a missing one. Never compare a request `UserId` and never answer 403 `UserErrors.Unauthorized()` for ownership.
- `IDateTimeProvider` (from `SharedKernel`) for timestamps — never `DateTime.UtcNow` directly.
- **Cache**: if any query caches data this command changes, inject `HybridCache` and call `cache.RemoveAsync({Feature}CacheKeys.X(...), cancellationToken)` after saving — for **every** key affected.
- For a returning command (`ICommand<Guid>`), return the value directly — `Result<T>` has an implicit conversion: `return project.Id;`.

## Domain additions (if needed)

Behavior method on the entity — the rule, the state change and the event live together:

```csharp
public Result {Behavior}(DateTime occurredAt)
{
    if (/* already in the target state */)
    {
        return Result.Success();
    }

    if (/* invariant violated */)
    {
        return {Entity}Errors.{Reason}(Id);
    }

    // State change through private setters.
    {Behavior}At = occurredAt;

    Raise(new {Entity}{Behavior}DomainEvent(Id));

    return Result.Success();
}
```

Return `Result` when the rule can fail, `void` when it can't. Cover it in `app/tests/Application.UnitTests/{Feature}/{Entity}Tests.cs`.

Error factory on the existing `{Entity}Errors` class in `app/src/Domain/{Feature}/`. The code prefix is the **plural of the entity** — the one already used by that catalog (`{Entity}Errors` → `"{EntityPlural}.*"`, `UserErrors` → `"Users.*"`), not the feature folder name:

```csharp
public static Error {Reason}(Guid {entity}Id) => Error.Problem(
    "{EntityPlural}.{Reason}",
    $"The {entity} with Id = '{{entity}Id}' <explains the violated rule>.");
```

Error type → HTTP status (via `CustomResults.Problem`): `NotFound` → 404, `Conflict` → 409, `Problem`/`Validation` → 400, `Unauthorized` → 401, `Forbidden` → 403, `Failure` → 500.

Domain event, one file each in `app/src/Domain/{Feature}/`:

```csharp
using SharedKernel;

namespace Domain.{Feature};

public sealed record {Entity}{Behavior}DomainEvent(Guid {Entity}Id) : IDomainEvent;
```

Optional event handler (Application layer, in the use-case folder):

```csharp
using Domain.{Feature};
using SharedKernel;

namespace Application.{Feature}.{UseCase};

internal sealed class {Entity}{Behavior}DomainEventHandler : IDomainEventHandler<{Entity}{Behavior}DomainEvent>
{
    public Task Handle({Entity}{Behavior}DomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // Side effects here (notifications, projections, ...)
        return Task.CompletedTask;
    }
}
```
