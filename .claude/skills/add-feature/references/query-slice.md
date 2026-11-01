# Query Slice Templates

Files go in `app/src/Application/{Feature}/{UseCase}/`. Queries are reads: no domain events, no `SaveChangesAsync`. Live models: `Todos/GetById/` (single item + cache) and `Todos/Get/` (paged list — but its `UserId` parameter is template legacy, see below).

> `Todos/Get` and `Users/GetById` take `UserId` from the request and answer 403 `UserErrors.Unauthorized()`. That is template legacy (`.claude/rules/security.md`) — new queries take the identity from `IUserContext` and never have a `UserId` parameter.

## Query

Single item:

```csharp
using Application.Abstractions.Messaging;

namespace Application.Todos.GetById;

public sealed record GetTodoByIdQuery(Guid TodoItemId) : IQuery<TodoResponse>;
```

List — always paged (`PagedList<T>`), never `List<T>`:

```csharp
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Todos.GetOverdue;

public sealed record GetOverdueTodosQuery(int Page, int PageSize) : IQuery<PagedList<TodoResponse>>;
```

## Validator

A query with input has a validator too — `ValidationDecorator` wraps queries as well as commands. Paging is always bounded (model: `Todos/Get/GetTodosQueryValidator.cs`):

```csharp
using FluentValidation;

namespace Application.Todos.GetOverdue;

internal sealed class GetOverdueTodosQueryValidator : AbstractValidator<GetOverdueTodosQuery>
{
    private const int MaxPageSize = 100;

    public GetOverdueTodosQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);

        RuleFor(q => q.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}
```

A query whose only input is a route id (e.g. `GetTodoByIdQuery`) needs no validator: the `:guid` route constraint already rejects malformed ids.

## Response DTO

Lives next to the query. Flat, serialization-friendly, never a domain entity.

```csharp
namespace Application.Todos.GetOverdue;

public sealed class TodoResponse
{
    public Guid Id { get; set; }
    public string Description { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

Each use-case folder owns its own `TodoResponse` — do not share DTOs across slices even if they look identical today. Return only the fields the use case needs (`.claude/rules/security.md`, LGPD).

## Handler

Scope to the current user, filter, order deterministically, project with `.Select` straight into the DTO, page with `PagedList<T>.CreateAsync`:

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.Todos.GetOverdue;

internal sealed class GetOverdueTodosQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetOverdueTodosQuery, PagedList<TodoResponse>>
{
    public async Task<Result<PagedList<TodoResponse>>> Handle(
        GetOverdueTodosQuery query,
        CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;

        IQueryable<TodoResponse> todosQuery = context.TodoItems
            .Where(todoItem => todoItem.UserId == userContext.UserId &&
                               !todoItem.IsCompleted &&
                               todoItem.DueDate < now)
            .OrderBy(todoItem => todoItem.DueDate)
            .Select(todoItem => new TodoResponse
            {
                Id = todoItem.Id,
                Description = todoItem.Description,
                DueDate = todoItem.DueDate,
                IsCompleted = todoItem.IsCompleted,
                CreatedAt = todoItem.CreatedAt
            });

        PagedList<TodoResponse> todos = await PagedList<TodoResponse>.CreateAsync(
            todosQuery,
            query.Page,
            query.PageSize,
            cancellationToken);

        return todos;
    }
}
```

- Single-item queries filter by id **and** `userContext.UserId`, and `return TodoItemErrors.NotFound(id);` when nothing matches — the catalog error converts implicitly to `Result<TodoResponse>` (404 also for another user's item).
- Free-text search uses `EF.Functions.ILike` with the term escaped — copy `EscapeLikePattern` from `Todos/Get/GetTodosQueryHandler.cs`, never concatenate SQL.
- A new filter/sort column ⇒ evaluate an index in the entity configuration (`.claude/rules/persistence.md`).

## Caching (optional, hot single-item reads only)

Wrap the database query in `HybridCache.GetOrCreateAsync` with a key from the feature's cache-keys class (live example: `GetTodoByIdQueryHandler`). Keys include the user id:

```csharp
namespace Application.Todos;

internal static class TodoCacheKeys
{
    internal static string ById(Guid userId, Guid todoItemId) => $"todos-{userId}-{todoItemId}";
}
```

```csharp
Guid userId = userContext.UserId;

TodoResponse? todo = await cache.GetOrCreateAsync(
    TodoCacheKeys.ById(userId, query.TodoItemId),
    async cancellation => await context.TodoItems
        .Where(todoItem => todoItem.Id == query.TodoItemId && todoItem.UserId == userId)
        .Select(...)
        .SingleOrDefaultAsync(cancellation),
    cancellationToken: cancellationToken);
```

Every command that mutates the cached data must invalidate the same key with `cache.RemoveAsync(...)`. If you can't enumerate the affected keys (paged lists, searches), don't cache.
