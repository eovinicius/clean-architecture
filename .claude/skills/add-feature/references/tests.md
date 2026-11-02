# Test Templates

Stack: xUnit + Shouldly + NSubstitute (unit), FluentValidation.TestHelper (validators), WebApplicationFactory + Testcontainers (integration). Every new use case gets all three. Placeholders as in [command-slice.md](command-slice.md). Live models: the closest analogous handler, validator and integration tests in `app/tests/Application.UnitTests/` and `app/tests/IntegrationTests/`.

## Handler unit tests

`app/tests/Application.UnitTests/{Feature}/{UseCase}{Command|Query}HandlerTests.cs`. Inherit `BaseHandlerTest` — it provides `CreateDbContext()` (in-memory `TestDbContext` implementing `IApplicationDbContext`) and `CreateCache()` (real `HybridCache`). Mock only interfaces (`IUserContext`, `IDateTimeProvider`) with NSubstitute; never mock the DbContext. Build the handler and seed data through private helpers so each test reads as the behavior it proves.

Cover: each failure path (one test per guard clause), the happy path (persisted state + raised domain event + cache invalidation) and **another user's resource → `NotFound`**.

```csharp
using Application.Abstractions.Authentication;
using Application.{Feature}.{UseCase};
using Application.UnitTests.Abstractions;
using Domain.{Feature};
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.UnitTests.{Feature};

public sealed class {Verb}{Entity}CommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact] // SPEC-NNNN AC-n
    public async Task Handle_Should_{Behavior}{Entity}AndRaiseDomainEvent_When{Condition}()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid {entity}Id = await Seed{Entity}Async(context, UserId);
        {Verb}{Entity}CommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new {Verb}{Entity}Command({entity}Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        {Entity} {entity} = await context.{EntityPlural}.SingleAsync(e => e.Id == {entity}Id);
        {entity}.{Behavior}At.ShouldBe(Now);
        {entity}.DomainEvents.ShouldContain(domainEvent => domainEvent is {Entity}{Behavior}DomainEvent);
    }

    [Fact] // SPEC-NNNN AC-n
    public async Task Handle_Should_ReturnNotFound_When{Entity}BelongsToAnotherUser()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid {entity}Id = await Seed{Entity}Async(context, Guid.NewGuid());
        {Verb}{Entity}CommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new {Verb}{Entity}Command({entity}Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe({Entity}Errors.NotFound({entity}Id));

        {Entity} {entity} = await context.{EntityPlural}.SingleAsync(e => e.Id == {entity}Id);
        {entity}.{Behavior}At.ShouldBeNull();
    }

    private static {Verb}{Entity}CommandHandler CreateHandler(TestDbContext context)
    {
        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(Now);

        return new {Verb}{Entity}CommandHandler(context, dateTimeProvider, userContext, cache);
    }

    private static async Task<Guid> Seed{Entity}Async(TestDbContext context, Guid ownerId)
    {
        // State is reached through the entity's own methods; then the setup events are dropped.
        var {entity} = {Entity}.Create(ownerId, /* required data */, DateTime.UtcNow);

        {entity}.ClearDomainEvents();

        context.{EntityPlural}.Add({entity});
        await context.SaveChangesAsync();

        return {entity}.Id;
    }
}
```

Conventions:

- Test names: `Handle_Should_{Outcome}_When{Condition}`.
- `// Arrange` / `// Act` / `// Assert` comments in every test; `// SPEC-NNNN AC-n` on the `[Fact]` when a spec exists.
- Assert failures by comparing the exact error (`result.Error.ShouldBe({Entity}Errors.NotFound(id))`) or its code (`result.Error.Code.ShouldBe("{EntityPlural}.{Reason}")`) — follow the neighboring test.
- Fixed instants (`Now`) instead of `DateTime.UtcNow` when the test asserts time.
- **Cache**: when the handler invalidates a key, prove it — prime the key through the real `HybridCache` from `CreateCache()`, run the command, and assert the next read misses.
- `GlobalUsings.cs` already imports `Xunit`, `NSubstitute` and `Shouldly` — don't re-add those usings. `SharedKernel` is **not** global: add `using SharedKernel;` when the test uses `Result`/`Error`.
- If the entity gains new properties, `TestDbContext` picks them up automatically; only touch it when adding a whole new `DbSet`.
- Seed through the entity factory and behavior methods, then `ClearDomainEvents()` — never object initializers, opened setters or reflection (`.claude/rules/testing.md`). A new behavior method also gets an `{Entity}Tests` test.
- Test classes and nested helper types are `sealed`.

## Validator tests

`app/tests/Application.UnitTests/{Feature}/{Validator}Tests.cs` for a single validator (e.g. `Get{X}QueryValidatorTests`), or extend the feature's shared validators test file if one exists.

```csharp
using Application.{Feature}.{UseCase};
using FluentValidation.TestHelper;

namespace Application.UnitTests.{Feature};

public sealed class Get{X}QueryValidatorTests
{
    private readonly Get{X}QueryValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validator_Should_HaveError_WhenPageSizeIsOutOfRange(int pageSize)
    {
        // Arrange
        Get{X}Query query = CreateQuery(page: 1, pageSize: pageSize);

        // Act
        TestValidationResult<Get{X}Query> result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(q => q.PageSize);
    }
}
```

Cover each rule's failure plus one fully-valid input (`ShouldNotHaveAnyValidationErrors`), including the limits (max length, max page size). Validators are `internal`: the test project already sees them via `InternalsVisibleTo`.

## Integration tests

`app/tests/IntegrationTests/{Feature}/{Feature}Tests.cs` or a file per capability (`{Entity}{Capability}Tests.cs`). Inherit `BaseIntegrationTest(factory)` — it runs the real API against a Testcontainers Postgres and provides `HttpClient`, `RegisterAndLoginAsync()`, and `Authenticate(token)`. Tests go through real HTTP, never call handlers directly. Declare private response records in the class (`{Entity}Dto`, `ProblemDto`).

```csharp
public sealed class {Entity}{Capability}Tests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record {Entity}Dto(Guid Id, DateTime? {Behavior}At);

    private sealed record ProblemDto(string Title, int Status);

    [Fact] // SPEC-NNNN AC-n
    public async Task {Verb}{Entity}_Should_{Outcome}()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid {entity}Id = await Create{Entity}Async();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"{feature}/{{entity}Id}/{verb}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        {Entity}Dto? {entity} = await HttpClient.GetFromJsonAsync<{Entity}Dto>($"{feature}/{{entity}Id}");
        {entity}!.{Behavior}At.ShouldNotBeNull();
    }

    [Fact]
    public async Task {Verb}{Entity}_Should_ReturnNotFound_When{Entity}BelongsToAnotherUser()
    {
        // Arrange
        (_, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid {entity}Id = await Create{Entity}Async();

        (_, AccessTokens otherTokens) = await RegisterAndLoginAsync();
        Authenticate(otherTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"{feature}/{{entity}Id}/{verb}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("{EntityPlural}.NotFound");
    }
}
```

Arrange helpers (`Create{Entity}Async`) live in the test class and call the existing endpoints. A new endpoint never asks for `userId` — only template-legacy endpoints still do (`.claude/rules/security.md`).

Minimum coverage per endpoint: one unauthorized test (no token → 401) if it's a new route family, one happy-path test asserting observable state via a follow-up GET, one failure translation test per handler failure (status + `Title` = error code), and one test that another user's resource answers 404.

## Run

```bash
dotnet test app/tests/Application.UnitTests
dotnet test app/tests/ArchitectureTests
dotnet test app/tests/IntegrationTests
```

Integration tests need Docker running (Testcontainers). Architecture tests will fail the build if a layer or convention rule is violated — fix the code, never the test.
