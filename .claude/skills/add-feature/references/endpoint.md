# Endpoint Templates

One file per use case in `app/src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`. Endpoints are `internal sealed`, implement `IEndpoint` and are auto-discovered by `AddEndpoints`/`MapEndpoints` — no registration needed. Live models: `Endpoints/Todos/Archive.cs`, `Endpoints/Todos/GetById.cs`.

> `Endpoints/Todos/Create.cs`, `Copy.cs` and `Get.cs` (and `Users/GetById.cs`) receive `userId` in the body/query string. That is template legacy — the identity comes from the token, so no request field, route segment or query parameter carries the current user's id.

## Command that creates a resource (POST → 201 + id)

```csharp
using Application.Abstractions.Messaging;
using Application.Projects.Create;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Projects;

internal sealed class Create : IEndpoint
{
    public sealed class Request
    {
        public string Name { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("projects", async (
            Request request,
            ICommandHandler<CreateProjectCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateProjectCommand(request.Name);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(id => Results.Created($"projects/{id}", id), CustomResults.Problem);
        })
        .WithTags(Tags.Projects)
        .WithSummary("Create a project")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithIdempotency()
        .RequireAuthorization();
    }
}
```

## Void command from route parameter (PUT/DELETE → 204)

```csharp
using Application.Abstractions.Messaging;
using Application.Todos.Archive;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Todos;

internal sealed class Archive : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("todos/{id:guid}/archive", async (
            Guid id,
            ICommandHandler<ArchiveTodoCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ArchiveTodoCommand(id);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .WithSummary("Archive a completed todo")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
```

## Paged query (GET → 200)

```csharp
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Todos.GetOverdue;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Todos;

internal sealed class GetOverdue : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("todos/overdue", async (
            int? page,
            int? pageSize,
            IQueryHandler<GetOverdueTodosQuery, PagedList<TodoResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetOverdueTodosQuery(page ?? 1, pageSize ?? 10);

            Result<PagedList<TodoResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .WithSummary("List the current user's overdue todos")
        .Produces<PagedList<TodoResponse>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .RequireAuthorization();
    }
}
```

## Rules

- Routes are lowercase, plural, no leading slash: `todos`, `todos/{id:guid}`, `todos/{id:guid}/archive`. Route constraints (`:guid`) on all typed parameters.
- The nested `Request` class exists only when there's a JSON body; it maps 1:1 to the command inside the lambda. Type enum properties with the domain enum (values still travel as numbers; the OpenAPI schema lists them). Don't put fields in the body that the route already carries — nor the current user's id.
- Success responses: creation → `Results.Created($"{route}/{id}", id)` + `.Produces<T>(StatusCodes.Status201Created)`; void command → `Results.NoContent` + `.Produces(StatusCodes.Status204NoContent)`; read or other returning command → `Results.Ok` + `.Produces<T>()`.
- Document the contract: `.WithSummary(...)`, the success `.Produces...`, and one `.ProducesProblem(status)` per status the handler's errors map to (Validation/Problem → 400, NotFound → 404, Conflict → 409). Use `ProducesProblem(400)`, not `ProducesValidationProblem()` — `CustomResults` emits `errors` as a list, not a dictionary. 401/403 for auth, 429 and the idempotency header/409 come from OpenAPI transformers — don't repeat them.
- POST that creates a resource adds `.WithIdempotency()` (opt-in `Idempotency-Key` header).
- Resolve the handler interface (`ICommandHandler<...>` / `IQueryHandler<...>`) directly as a lambda parameter — the decorated instance is injected.
- Always end with `.WithTags(Tags.{Feature})` and `.RequireAuthorization()` (or `.HasPermission(Permissions.X)` where a permission constant exists). Add the feature constant to `Tags.cs` if new. Anonymous only if intentional and justified in the PR (like `Users/Login`, `Users/Register`, `Users/RefreshToken`).
- Failures never get hand-rolled responses — `CustomResults.Problem` translates the `Error` to RFC 7807 ProblemDetails with the right status code.
