# Query Slice Templates

Files go in `app/src/Application/{Feature}/{UseCase}/`. Queries are reads: no domain events, no `IUnitOfWork`. Placeholders as in [command-slice.md](command-slice.md); `{X}` names what the query returns. Live models: the closest analogous `GetById` (single item + cache) and paged-list slices in `app/src/Application/`.

> Some template queries take `UserId` from the request and answer 403 `UserErrors.Unauthorized()`. That is template legacy (`.claude/rules/security.md`) — new queries take the identity from `IUserContext` and never have a `UserId` parameter.

## Query

Single item:

```csharp
using Application.Abstractions.Messaging;

namespace Application.{Feature}.GetById;

public sealed record Get{Entity}ByIdQuery(Guid {Entity}Id) : IQuery<{Entity}Response>;
```

List — always paged (`PagedList<T>`), never `List<T>`:

```csharp
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.{Feature}.{UseCase};

public sealed record Get{X}Query(int Page, int PageSize) : IQuery<PagedList<{Entity}Response>>;
```

## Validator

A query with input has a validator too — `ValidationDecorator` wraps queries as well as commands. Paging is always bounded (mirror an existing paged-list query validator):

```csharp
using FluentValidation;

namespace Application.{Feature}.{UseCase};

internal sealed class Get{X}QueryValidator : AbstractValidator<Get{X}Query>
{
    private const int MaxPageSize = 100;

    public Get{X}QueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);

        RuleFor(q => q.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}
```

A query whose only input is a route id (e.g. `Get{Entity}ByIdQuery`) needs no validator: the `:guid` route constraint already rejects malformed ids.

## Response DTO

Lives next to the query. Flat, serialization-friendly, never a domain entity. It maps itself from the entity with an `internal static From` factory, so the handler only orchestrates:

```csharp
using Domain.{Feature};

namespace Application.{Feature}.{UseCase};

public sealed class {Entity}Response
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }

    internal static {Entity}Response From({Entity} {entity}) => new()
    {
        Id = {entity}.Id,
        Name = {entity}.Name,
        DueDate = {entity}.DueDate,
        CreatedAt = {entity}.CreatedAt
    };
}
```

Each use-case folder owns its own `{Entity}Response` (and its `From` mapping) — do not share DTOs across slices even if they look identical today: they change for different reasons. Return only the fields the use case needs (`.claude/rules/security.md`, LGPD).

## Handler

Data comes from `I{Entity}Repository` (`Application.Abstractions.Data`) — no EF Core in the Application. Filter, order and paging are passed down to the repository (a `{Entity}ListFilter` record next to the interface when there are several criteria); the handler maps the page with `PagedList<T>.Map`:

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.{Feature};
using SharedKernel;

namespace Application.{Feature}.{UseCase};

internal sealed class Get{X}QueryHandler(
    I{Entity}Repository {entity}Repository,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<Get{X}Query, PagedList<{Entity}Response>>
{
    public async Task<Result<PagedList<{Entity}Response>>> Handle(
        Get{X}Query query,
        CancellationToken cancellationToken)
    {
        PagedList<{Entity}> page = await {entity}Repository.GetDuePagedAsync(
            userContext.UserId,
            dateTimeProvider.UtcNow,
            query.Page,
            query.PageSize,
            cancellationToken);

        return page.Map({Entity}Response.From);
    }
}
```

Repository side (`app/src/Infrastructure/Database/Repositories/{Entity}Repository.cs`): scope to the user, filter, order deterministically, read with `AsNoTracking()` and page with the `ToPagedListAsync` extension (`app/src/Infrastructure/Database/QueryableExtensions.cs`):

```csharp
public Task<PagedList<{Entity}>> GetDuePagedAsync(
    Guid userId,
    DateTime utcNow,
    int page,
    int pageSize,
    CancellationToken cancellationToken = default) =>
    context.{EntityPlural}
        .AsNoTracking()
        .Where({entity} => {entity}.UserId == userId && {entity}.DueDate < utcNow)
        .OrderBy({entity} => {entity}.DueDate)
        .ToPagedListAsync(page, pageSize, cancellationToken);
```

- Single-item queries load through the repository filtered by id **and** `userContext.UserId`, and `return {Entity}Errors.NotFound(id);` when nothing comes back — the catalog error converts implicitly to `Result<{Entity}Response>` (404 also for another user's item).
- Free-text search lives in the repository and uses `EF.Functions.ILike` with the term escaped — reuse the `EscapeLikePattern` helper in `app/src/Infrastructure/Database/Repositories/TodoItemRepository.cs`, never concatenate SQL.
- A new filter/sort column ⇒ evaluate an index in the entity configuration (`.claude/rules/persistence.md`).

## Caching (optional, hot single-item reads only)

Wrap the repository call in `HybridCache.GetOrCreateAsync` with a key from the feature's cache-keys class (`{Feature}CacheKeys`, next to the feature folder). Keys include the user id:

```csharp
namespace Application.{Feature};

internal static class {Feature}CacheKeys
{
    internal static string ById(Guid userId, Guid {entity}Id) => $"{feature}-{userId}-{{entity}Id}";
}
```

```csharp
Guid userId = userContext.UserId;

{Entity}Response? response = await cache.GetOrCreateAsync(
    {Feature}CacheKeys.ById(userId, query.{Entity}Id),
    async cancellation =>
        await {entity}Repository.GetByIdAsync(query.{Entity}Id, userId, cancellation) is { } {entity}
            ? {Entity}Response.From({entity})
            : null,
    cancellationToken: cancellationToken);
```

Every command that mutates the cached data must invalidate the same key with `cache.RemoveAsync(...)`. If you can't enumerate the affected keys (paged lists, searches), don't cache.
