# Command Slice Templates

Files go in `app/src/Application/{Feature}/{UseCase}/`. Replace `{Feature}` (plural, e.g. `Todos`), `{Entity}` (e.g. `TodoItem`), and use-case names throughout. Live model: `app/src/Application/Todos/Archive/`.

> `Todos/Create` and `Todos/Copy` carry `UserId` in the command and answer 403 `UserErrors.Unauthorized()`. That is template legacy (`.claude/rules/60-security.md`) — never copy their shape.

## Command

Positional record for few parameters:

```csharp
using Application.Abstractions.Messaging;

namespace Application.Todos.Archive;

public sealed record ArchiveTodoCommand(Guid TodoItemId) : ICommand;
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

`internal sealed`, primary constructor, `IApplicationDbContext` for data access. The handler **orchestrates**: it loads the entity filtered by owner (missing → `NotFound`), calls the entity's behavior method — which enforces the rule, returns `Result` and raises the domain event — propagates a failure, saves, invalidates the cache, and returns. It never assigns properties nor calls `Raise` (`.claude/rules/10-architecture.md`, Domain). This is the real `ArchiveTodoCommandHandler`; the rule lives in `TodoItem.Archive` (`app/src/Domain/Todos/TodoItem.cs`):

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Todos.Archive;

internal sealed class ArchiveTodoCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IUserContext userContext,
    HybridCache cache)
    : ICommandHandler<ArchiveTodoCommand>
{
    public async Task<Result> Handle(ArchiveTodoCommand command, CancellationToken cancellationToken)
    {
        TodoItem? todoItem = await context.TodoItems
            .SingleOrDefaultAsync(t => t.Id == command.TodoItemId && t.UserId == userContext.UserId, cancellationToken);

        if (todoItem is null)
        {
            return Result.Failure(TodoItemErrors.NotFound(command.TodoItemId));
        }

        Result result = todoItem.Archive(dateTimeProvider.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(TodoCacheKeys.ById(todoItem.UserId, todoItem.Id), cancellationToken);

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
- A business rule from a spec gets a pt-BR comment citing it (`// SPEC-0001 RN-4: ...`) on the entity method that implements it.

## Domain additions (if needed)

Behavior method on the entity — the rule, the state change and the event live together. This is the real `TodoItem.Archive`:

```csharp
public Result Archive(DateTime archivedAt)
{
    // SPEC-0001 RN-4: arquivar é idempotente e preserva o ArchivedAt original.
    if (IsArchived)
    {
        return Result.Success();
    }

    if (!IsCompleted)
    {
        return Result.Failure(TodoItemErrors.NotCompleted(Id));
    }

    IsArchived = true;
    ArchivedAt = archivedAt;

    Raise(new TodoItemArchivedDomainEvent(Id));

    return Result.Success();
}
```

Return `Result` when the rule can fail, `void` when it can't (`Unarchive`). Cover it in `app/tests/Application.UnitTests/{Feature}/{Entity}Tests.cs` (model: `TodoItemTests.cs`).

Error factory on the existing `{Entity}Errors` class in `app/src/Domain/{Feature}/`. The code prefix is the **plural of the entity** — the one already used by that catalog (`TodoItemErrors` → `"TodoItems.*"`, `UserErrors` → `"Users.*"`), not the feature folder name:

```csharp
public static Error NotCompleted(Guid todoItemId) => Error.Problem(
    "TodoItems.NotCompleted",
    $"The todo item with Id = '{todoItemId}' must be completed before it can be archived.");
```

Error type → HTTP status (via `CustomResults.Problem`): `NotFound` → 404, `Conflict` → 409, `Problem`/`Validation` → 400, `Unauthorized` → 401, `Forbidden` → 403, `Failure` → 500.

Domain event, one file each in `app/src/Domain/{Feature}/`:

```csharp
using SharedKernel;

namespace Domain.Todos;

public sealed record TodoItemArchivedDomainEvent(Guid TodoItemId) : IDomainEvent;
```

Optional event handler (Application layer, in the use-case folder):

```csharp
using Domain.Todos;
using SharedKernel;

namespace Application.Todos.Archive;

internal sealed class TodoItemArchivedDomainEventHandler : IDomainEventHandler<TodoItemArchivedDomainEvent>
{
    public Task Handle(TodoItemArchivedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // Side effects here (notifications, projections, ...)
        return Task.CompletedTask;
    }
}
```
