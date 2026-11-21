# Controller Action Templates

One controller per feature in `app/src/Api/Controllers/{Feature}Controller.cs` (`public sealed`, `ControllerBase`, `[ApiController]`, `[Route("{feature}")]`, `[Tags(Tags.{Feature})]`, `[Authorize]`), **one action per use case**. Controllers are discovered by `MapControllers` — no registration needed. A new feature creates its controller; an existing feature gets a new action. Placeholders as in [command-slice.md](command-slice.md); `{feature}` is the lowercase plural route segment. Owner of the convention: `.claude/rules/architecture.md` (Api). Live models: `app/src/Api/Controllers/TodosController.cs` and `UsersController.cs`.

> Some template actions receive `userId` in the body/query string. That is template legacy — the identity comes from the token, so no request field, route segment or query parameter carries the current user's id.

## New controller

```csharp
using System.Net.Mime;
using Api.Controllers.Projects;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Projects.Create;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.Controllers;

[ApiController]
[Route("projects")]
[Tags(Tags.Projects)]
[Authorize]
public sealed class ProjectsController : ControllerBase
{
    // actions below
}
```

Add the feature constant to `app/src/Api/Controllers/Tags.cs` if new.

## Request body

One file per request in `app/src/Api/Controllers/{Feature}/{UseCase}Request.cs`. The OpenAPI schema id becomes `{Feature}{UseCase}Request` (`Api/Extensions/ServiceCollectionExtensions.cs`). It exists only when there's a JSON body and maps 1:1 to the command:

```csharp
namespace Api.Controllers.Projects;

public sealed record CreateRequest(string Name);
```

Type enum properties with the domain enum (values travel as numbers; the schema lists them). Don't put fields in the body that the route already carries — nor the current user's id.

## Command that creates a resource (POST → 201 + id)

```csharp
[HttpPost]
[Idempotent]
[EndpointSummary("Create a project")]
[ProducesResponseType<Guid>(StatusCodes.Status201Created)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
public async Task<IResult> Create(
    CreateRequest request,
    [FromServices] ICommandHandler<CreateProjectCommand, Guid> handler,
    CancellationToken cancellationToken)
{
    var command = new CreateProjectCommand(request.Name);

    Result<Guid> result = await handler.Handle(command, cancellationToken);

    return result.Match(id => Results.Created($"projects/{id}", id), CustomResults.Problem);
}
```

## Void command from route parameter (PUT/DELETE → 204)

```csharp
[HttpPut("{id:guid}/{verb}")]
[EndpointSummary("{Verb} a {entity}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
public async Task<IResult> {Verb}(
    Guid id,
    [FromServices] ICommandHandler<{Verb}{Entity}Command> handler,
    CancellationToken cancellationToken)
{
    var command = new {Verb}{Entity}Command(id);

    Result result = await handler.Handle(command, cancellationToken);

    return result.Match(Results.NoContent, CustomResults.Problem);
}
```

## Paged query (GET → 200)

```csharp
[HttpGet("{segment}")]
[EndpointSummary("List the current user's {X}")]
[ProducesResponseType<PagedList<{Entity}Response>>(StatusCodes.Status200OK)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
public async Task<IResult> Get{X}(
    [FromQuery] int? page,
    [FromQuery] int? pageSize,
    [FromServices] IQueryHandler<Get{X}Query, PagedList<{Entity}Response>> handler,
    CancellationToken cancellationToken)
{
    var query = new Get{X}Query(page ?? 1, pageSize ?? 10);

    Result<PagedList<{Entity}Response>> result = await handler.Handle(query, cancellationToken);

    return result.Match(Results.Ok, CustomResults.Problem);
}
```

## Rules

- Routes are lowercase, plural, no leading slash; the controller carries `[Route("{feature}")]` and the action the rest (`{id:guid}`, `{id:guid}/{verb}`). Route constraints (`:guid`) on all typed parameters. A required query value without a default uses `[FromQuery, BindRequired]`.
- Success responses: creation → `Results.Created($"{route}/{id}", id)` + `[ProducesResponseType<T>(StatusCodes.Status201Created)]`; void command → `Results.NoContent` + `[ProducesResponseType(StatusCodes.Status204NoContent)]`; read or other returning command → `Results.Ok` + `[ProducesResponseType<T>(StatusCodes.Status200OK)]`.
- Document the contract: `[EndpointSummary(...)]`, the success `[ProducesResponseType...]`, and one `[ProducesResponseType<ProblemDetails>(status, MediaTypeNames.Application.ProblemJson)]` per status the handler's errors map to (Validation/Problem → 400, NotFound → 404, Conflict → 409). 401/403 for auth, 429 and the idempotency header/409 come from OpenAPI transformers — don't repeat them.
- POST that creates a resource adds `[Idempotent]` (opt-in `Idempotency-Key` header).
- Inject the handler interface (`ICommandHandler<...>` / `IQueryHandler<...>`) with `[FromServices]` on the action — the decorated instance is injected; the controller has no constructor dependencies.
- Authorization: the controller's `[Authorize]` covers every action; a permission adds `[HasPermission(Permissions.X)]` (`app/src/Api/Controllers/Permissions.cs`). Anonymous only with `[AllowAnonymous]`, intentional and justified in the PR (like `Users/Login`, `Users/Register`), and listed in `IntegrationTests/Api/EndpointAuthorizationTests.cs`. Public auth flows also add `[EnableRateLimiting(RateLimitingPolicies.Authentication)]`.
- Failures never get hand-rolled responses — `CustomResults.Problem` translates the `Error` to RFC 7807 ProblemDetails with the right status code.
- The contract guard is `IntegrationTests/Api/OpenApiDocumentTests.cs`: add a case for a new request body or status.
