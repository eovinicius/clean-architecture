# Endpoint Templates

One file per use case in `app/src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`. Endpoints implement `IEndpoint` and are auto-discovered by `AddEndpoints`/`MapEndpoints` — no registration needed.

## Command with response body (POST → 200 + value)

```csharp
using Application.Abstractions.Messaging;
using Application.Todos.Create;
using Domain.Todos;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Todos;

internal sealed class Create : IEndpoint
{
    public sealed class Request
    {
        public Guid UserId { get; set; }
        public string Description { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string> Labels { get; set; } = [];
        public Priority Priority { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("todos", async (
            Request request,
            ICommandHandler<CreateTodoCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateTodoCommand
            {
                UserId = request.UserId,
                Description = request.Description,
                DueDate = request.DueDate,
                Labels = request.Labels,
                Priority = request.Priority
            };

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .WithSummary("Create a todo")
        .Produces<Guid>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
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
        .WithSummary("Archive a todo")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
```

## Query (GET → 200)

```csharp
using Application.Abstractions.Messaging;
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
            IQueryHandler<GetOverdueTodosQuery, List<TodoResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetOverdueTodosQuery();

            Result<List<TodoResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .WithSummary("List overdue todos")
        .Produces<List<TodoResponse>>()
        .RequireAuthorization();
    }
}
```

## Rules

- Routes are lowercase, plural, no leading slash: `todos`, `todos/{id:guid}`, `users/{userId:guid}/todos`. Route constraints (`:guid`) on all typed parameters.
- The nested `Request` class exists only when there's a JSON body; it maps 1:1 to the command inside the lambda. Type enum properties with the domain enum (values still travel as numbers; the OpenAPI schema lists them). Don't put fields in the body that the route already carries.
- Document the contract: `.WithSummary(...)`, `.Produces<T>(status)` (or `.Produces(204)`), and one `.ProducesProblem(status)` per status the handler's errors map to (Validation/Problem → 400, Forbidden → 403, NotFound → 404, Conflict → 409). Use `ProducesProblem(400)`, not `ProducesValidationProblem()` — `CustomResults` emits `errors` as a list, not a dictionary. 401/403 for auth, 429 and the idempotency header/409 come from OpenAPI transformers.
- Resolve the handler interface (`ICommandHandler<...>` / `IQueryHandler<...>`) directly as a lambda parameter — the decorated instance is injected.
- Always end with `.WithTags(Tags.{Feature})` and `.RequireAuthorization()` (or `.HasPermission(Permissions.X)` where a permission constant exists). Add the feature constant to `Tags.cs` if new.
- Failures never get hand-rolled responses — `CustomResults.Problem` translates the `Error` to RFC 7807 ProblemDetails with the right status code.
