# Query Slice Templates

Files go in `app/src/Application/{Feature}/{UseCase}/`. Queries are reads: no domain events, no `SaveChangesAsync`. Placeholders as in [command-slice.md](command-slice.md); `{X}` names what the query returns. Live models: the closest analogous `GetById` (single item + cache) and paged-list slices in `app/src/Application/`.

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

Lives next to the query. Flat, serialization-friendly, never a domain entity.

```csharp
namespace Application.{Feature}.{UseCase};

public sealed class {Entity}Response
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

Each use-case folder owns its own `{Entity}Response` — do not share DTOs across slices even if they look identical today. Return only the fields the use case needs (`.claude/rules/security.md`, LGPD).

## Handler

Scope to the current user, filter, order deterministically, project with `.Select` straight into the DTO, page with `PagedList<T>.CreateAsync`:

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.{Feature}.{UseCase};

internal sealed class Get{X}QueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<Get{X}Query, PagedList<{Entity}Response>>
{
    public async Task<Result<PagedList<{Entity}Response>>> Handle(
        Get{X}Query query,
        CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;

        IQueryable<{Entity}Response> {entity}Query = context.{EntityPlural}
            .Where({entity} => {entity}.UserId == userContext.UserId &&
                               {entity}.DueDate < now)
            .OrderBy({entity} => {entity}.DueDate)
            .Select({entity} => new {Entity}Response
            {
                Id = {entity}.Id,
                Name = {entity}.Name,
                DueDate = {entity}.DueDate,
                CreatedAt = {entity}.CreatedAt
            });

        PagedList<{Entity}Response> page = await PagedList<{Entity}Response>.CreateAsync(
            {entity}Query,
            query.Page,
            query.PageSize,
            cancellationToken);

        return page;
    }
}
```

- Single-item queries filter by id **and** `userContext.UserId`, and `return {Entity}Errors.NotFound(id);` when nothing matches — the catalog error converts implicitly to `Result<{Entity}Response>` (404 also for another user's item).
- Free-text search uses `EF.Functions.ILike` with the term escaped — reuse the existing `EscapeLikePattern` helper in the Application layer, never concatenate SQL.
- A new filter/sort column ⇒ evaluate an index in the entity configuration (`.claude/rules/persistence.md`).

## Caching (optional, hot single-item reads only)

Wrap the database query in `HybridCache.GetOrCreateAsync` with a key from the feature's cache-keys class (`{Feature}CacheKeys`, next to the feature folder). Keys include the user id:

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
    async cancellation => await context.{EntityPlural}
        .Where({entity} => {entity}.Id == query.{Entity}Id && {entity}.UserId == userId)
        .Select(...)
        .SingleOrDefaultAsync(cancellation),
    cancellationToken: cancellationToken);
```

Every command that mutates the cached data must invalidate the same key with `cache.RemoveAsync(...)`. If you can't enumerate the affected keys (paged lists, searches), don't cache.
