using System.Net.Mime;
using Api.Controllers.Todos;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Todos.Archive;
using Application.Todos.Complete;
using Application.Todos.Copy;
using Application.Todos.Create;
using Application.Todos.Delete;
using Application.Todos.Get;
using Application.Todos.GetById;
using Application.Todos.Unarchive;
using Application.Todos.Update;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SharedKernel;
using GetTodoByIdResponse = Application.Todos.GetById.TodoResponse;
using GetTodosResponse = Application.Todos.Get.TodoResponse;

namespace Api.Controllers;

[ApiController]
[Route("todos")]
[Tags(Tags.Todos)]
[Authorize]
public sealed class TodosController : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List a user's todos (search, sort, paging)")]
    [ProducesResponseType<PagedList<GetTodosResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> Get(
        [FromQuery, BindRequired] Guid userId,
        [FromQuery] string? search,
        [FromQuery] string? sortColumn,
        [FromQuery] string? sortOrder,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] bool? includeArchived,
        [FromServices] IQueryHandler<GetTodosQuery, PagedList<GetTodosResponse>> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetTodosQuery(
            userId,
            search,
            sortColumn,
            sortOrder,
            page ?? 1,
            pageSize ?? 10,
            includeArchived ?? false);

        Result<PagedList<GetTodosResponse>> result = await handler.Handle(query, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get a todo by id")]
    [ProducesResponseType<GetTodoByIdResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> GetById(
        Guid id,
        [FromServices] IQueryHandler<GetTodoByIdQuery, GetTodoByIdResponse> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetTodoByIdQuery(id);

        Result<GetTodoByIdResponse> result = await handler.Handle(query, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpPost]
    [Idempotent]
    [EndpointSummary("Create a todo")]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> Create(
        CreateRequest request,
        [FromServices] ICommandHandler<CreateTodoCommand, Guid> handler,
        CancellationToken cancellationToken)
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

        return result.Match(id => Results.Created($"todos/{id}", id), CustomResults.Problem);
    }

    [HttpPost("{todoId:guid}/copy")]
    [EndpointSummary("Copy a todo")]
    [ProducesResponseType<Guid>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> Copy(
        Guid todoId,
        CopyRequest request,
        [FromServices] ICommandHandler<CopyTodoCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var command = new CopyTodoCommand
        {
            UserId = request.UserId,
            TodoId = todoId
        };

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpPut("{id:guid}")]
    [EndpointSummary("Update a todo's description")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> Update(
        Guid id,
        UpdateRequest request,
        [FromServices] ICommandHandler<UpdateTodoCommand> handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTodoCommand(id, request.Description);

        Result result = await handler.Handle(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpPut("{id:guid}/complete")]
    [EndpointSummary("Mark a todo as completed")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> Complete(
        Guid id,
        [FromServices] ICommandHandler<CompleteTodoCommand> handler,
        CancellationToken cancellationToken)
    {
        var command = new CompleteTodoCommand(id);

        Result result = await handler.Handle(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpPut("{id:guid}/archive")]
    [EndpointSummary("Archive a completed todo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> Archive(
        Guid id,
        [FromServices] ICommandHandler<ArchiveTodoCommand> handler,
        CancellationToken cancellationToken)
    {
        var command = new ArchiveTodoCommand(id);

        Result result = await handler.Handle(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpPut("{id:guid}/unarchive")]
    [EndpointSummary("Unarchive a todo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> Unarchive(
        Guid id,
        [FromServices] ICommandHandler<UnarchiveTodoCommand> handler,
        CancellationToken cancellationToken)
    {
        var command = new UnarchiveTodoCommand(id);

        Result result = await handler.Handle(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpDelete("{id:guid}")]
    [EndpointSummary("Delete a todo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteTodoCommand> handler,
        CancellationToken cancellationToken)
    {
        var command = new DeleteTodoCommand(id);

        Result result = await handler.Handle(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }
}
