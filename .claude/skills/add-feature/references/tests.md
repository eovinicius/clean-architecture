# Test Templates

Stack: xUnit + Shouldly + NSubstitute (unit), FluentValidation.TestHelper (validators), WebApplicationFactory + Testcontainers (integration). Every new use case gets all three. Live models: `Application.UnitTests/Todos/ArchiveTodoCommandHandlerTests.cs`, `GetTodosQueryValidatorTests.cs`, `IntegrationTests/Todos/TodoArchivingTests.cs`.

## Handler unit tests

`app/tests/Application.UnitTests/{Feature}/{UseCase}{Command|Query}HandlerTests.cs`. Inherit `BaseHandlerTest` — it provides `CreateDbContext()` (in-memory `TestDbContext` implementing `IApplicationDbContext`) and `CreateCache()` (real `HybridCache`). Mock only interfaces (`IUserContext`, `IDateTimeProvider`) with NSubstitute; never mock the DbContext. Build the handler and seed data through private helpers so each test reads as the behavior it proves.

Cover: each failure path (one test per guard clause), the happy path (persisted state + raised domain event + cache invalidation) and **another user's resource → `NotFound`**.

```csharp
using Application.Abstractions.Authentication;
using Application.Todos.Archive;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class ArchiveTodoCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact] // SPEC-0001 AC-1
    public async Task Handle_Should_ArchiveTodoAndRaiseDomainEvent_WhenTodoIsCompleted()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, UserId, isCompleted: true);
        ArchiveTodoCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new ArchiveTodoCommand(todoItemId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.IsArchived.ShouldBeTrue();
        todoItem.ArchivedAt.ShouldBe(Now);
        todoItem.DomainEvents.ShouldContain(domainEvent => domainEvent is TodoItemArchivedDomainEvent);
    }

    [Fact] // SPEC-0001 AC-14
    public async Task Handle_Should_ReturnNotFound_WhenTodoBelongsToAnotherUser()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, Guid.NewGuid(), isCompleted: true);
        ArchiveTodoCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new ArchiveTodoCommand(todoItemId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TodoItemErrors.NotFound(todoItemId));

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.IsArchived.ShouldBeFalse();
    }

    private static ArchiveTodoCommandHandler CreateHandler(TestDbContext context)
    {
        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(Now);

        return new ArchiveTodoCommandHandler(context, dateTimeProvider, userContext, cache);
    }

    private static async Task<Guid> SeedTodoAsync(TestDbContext context, Guid ownerId, bool isCompleted)
    {
        // State is reached through the entity's own methods; then the setup events are dropped.
        var todoItem = TodoItem.Create(ownerId, "Existing todo", Priority.Low, null, [], DateTime.UtcNow);

        if (isCompleted)
        {
            todoItem.Complete(DateTime.UtcNow);
        }

        todoItem.ClearDomainEvents();

        context.TodoItems.Add(todoItem);
        await context.SaveChangesAsync();

        return todoItem.Id;
    }
}
```

Conventions:
- Test names: `Handle_Should_{Outcome}_When{Condition}`.
- `// Arrange` / `// Act` / `// Assert` comments in every test; `// SPEC-NNNN AC-n` on the `[Fact]` when a spec exists.
- Assert failures by comparing the exact error (`result.Error.ShouldBe(TodoItemErrors.NotFound(id))`) or its code (`result.Error.Code.ShouldBe("TodoItems.NotCompleted")`) — follow the neighboring test.
- Fixed instants (`Now`) instead of `DateTime.UtcNow` when the test asserts time.
- **Cache**: when the handler invalidates a key, prove it — prime the key through the real `HybridCache` from `CreateCache()`, run the command, and assert the next read misses.
- `GlobalUsings.cs` already imports `Xunit`, `NSubstitute`, `Shouldly`, and `SharedKernel` — don't re-add those usings.
- If the entity gains new properties, `TestDbContext` picks them up automatically; only touch it when adding a whole new `DbSet`.
- Seed through the entity factory and behavior methods, then `ClearDomainEvents()` — never object initializers, opened setters or reflection (`.claude/rules/30-testing.md`). A new behavior method also gets an `{Entity}Tests` test.
- Test classes and nested helper types are `sealed`.

## Validator tests

`app/tests/Application.UnitTests/{Feature}/{Validator}Tests.cs` for a single validator (e.g. `GetTodosQueryValidatorTests`), or extend the feature's shared file if one exists (`TodoValidatorsTests`).

```csharp
using Application.Todos.Get;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Todos;

public sealed class GetTodosQueryValidatorTests
{
    private readonly GetTodosQueryValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validator_Should_HaveError_WhenPageSizeIsOutOfRange(int pageSize)
    {
        // Arrange
        GetTodosQuery query = CreateQuery(page: 1, pageSize: pageSize);

        // Act
        TestValidationResult<GetTodosQuery> result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(q => q.PageSize);
    }
}
```

Cover each rule's failure plus one fully-valid input (`ShouldNotHaveAnyValidationErrors`), including the limits (max length, max page size). Validators are `internal`: the test project already sees them via `InternalsVisibleTo`.

## Integration tests

`app/tests/IntegrationTests/{Feature}/{Feature}Tests.cs` or a file per capability (`TodoArchivingTests.cs`). Inherit `BaseIntegrationTest(factory)` — it runs the real API against a Testcontainers Postgres and provides `HttpClient`, `RegisterAndLoginAsync()`, and `Authenticate(token)`. Tests go through real HTTP, never call handlers directly. Declare private response records in the class (`TodoDto`, `ProblemDto`).

```csharp
public sealed class TodoArchivingTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record TodoDto(Guid Id, bool IsArchived, DateTime? ArchivedAt);

    private sealed record ProblemDto(string Title, int Status);

    [Fact] // SPEC-0001 AC-1
    public async Task ArchiveTodo_Should_ArchiveCompletedTodo()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid todoId = await CreateCompletedTodoAsync(userId);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"todos/{todoId}/archive", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        TodoDto? todo = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");
        todo!.IsArchived.ShouldBeTrue();
    }

    [Fact]
    public async Task ArchiveTodo_Should_ReturnNotFound_WhenTodoBelongsToAnotherUser()
    {
        // Arrange
        (Guid ownerId, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid todoId = await CreateCompletedTodoAsync(ownerId);

        (_, AccessTokens otherTokens) = await RegisterAndLoginAsync();
        Authenticate(otherTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"todos/{todoId}/archive", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("TodoItems.NotFound");
    }
}
```

Arrange helpers (`CreateTodoAsync`, `CreateCompletedTodoAsync`) live in the test class and call the existing endpoints. They send `userId` only because the legacy `POST todos` still requires it — a new endpoint never asks for it.

Minimum coverage per endpoint: one unauthorized test (no token → 401) if it's a new route family, one happy-path test asserting observable state via a follow-up GET, one failure translation test per handler failure (status + `Title` = error code), and one test that another user's resource answers 404.

## Run

```
dotnet test app/tests/Application.UnitTests
dotnet test app/tests/ArchitectureTests
dotnet test app/tests/IntegrationTests
```

Integration tests need Docker running (Testcontainers). Architecture tests will fail the build if a layer or convention rule is violated — fix the code, never the test.
