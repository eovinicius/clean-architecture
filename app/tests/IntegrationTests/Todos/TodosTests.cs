using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Todos;

public sealed class TodosTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record TodoDto(Guid Id, Guid UserId, string Description, bool IsCompleted);

    [Fact]
    public async Task GetTodo_Should_ReturnUnauthorized_WhenTokenIsMissing()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"todos/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTodo_Should_PersistTodo_ThatCanBeRetrievedById()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        var createRequest = new
        {
            userId,
            description = "Integration test todo",
            labels = new[] { "integration" },
            priority = 2
        };

        // Act
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("todos", createRequest);

        // Assert
        createResponse.EnsureSuccessStatusCode();
        Guid todoId = await createResponse.Content.ReadFromJsonAsync<Guid>();
        todoId.ShouldNotBe(Guid.Empty);

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"todos/{todoId}");
        getResponse.EnsureSuccessStatusCode();

        TodoDto? todo = await getResponse.Content.ReadFromJsonAsync<TodoDto>();
        todo!.Id.ShouldBe(todoId);
        todo.UserId.ShouldBe(userId);
        todo.Description.ShouldBe("Integration test todo");
        todo.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateTodo_Should_Succeed_WhenDescriptionHasMaxLength()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        string description = new('a', 500);

        // Act
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync(
            "todos",
            new { userId, description, labels = Array.Empty<string>(), priority = 1 });

        // Assert
        createResponse.EnsureSuccessStatusCode();
        Guid todoId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        TodoDto? todo = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");
        todo!.Description.ShouldBe(description);
    }

    [Fact]
    public async Task CreateTodo_Should_ReturnCreated_WithLocationOfTheTodo()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "todos",
            new { userId, description = "Located todo", labels = Array.Empty<string>(), priority = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid todoId = await response.Content.ReadFromJsonAsync<Guid>();
        response.Headers.Location!.ToString().ShouldBe($"todos/{todoId}");

        HttpResponseMessage getResponse = await HttpClient.GetAsync(response.Headers.Location);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateTodo_Should_ChangeDescription()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid todoId = await CreateTodoAsync(userId, "Original description");

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"todos/{todoId}", new { description = "Updated description" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        TodoDto? todo = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");
        todo!.Description.ShouldBe("Updated description");
    }

    [Fact]
    public async Task DeleteTodo_Should_RemoveTodo()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid todoId = await CreateTodoAsync(userId, "Todo to delete");

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"todos/{todoId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        HttpResponseMessage getResponse = await HttpClient.GetAsync($"todos/{todoId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CopyTodo_Should_CreateNewTodoWithSameDescription()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid todoId = await CreateTodoAsync(userId, "Todo to copy");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync($"todos/{todoId}/copy", new { userId });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Guid copyId = await response.Content.ReadFromJsonAsync<Guid>();
        copyId.ShouldNotBe(todoId);

        TodoDto? copy = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{copyId}");
        copy!.Description.ShouldBe("Todo to copy");
    }

    [Fact]
    public async Task TodoMutations_Should_ReturnNotFound_WhenTodoDoesNotExist()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        var missingId = Guid.NewGuid();

        // Act
        HttpResponseMessage update = await HttpClient.PutAsJsonAsync($"todos/{missingId}", new { description = "Nope" });
        HttpResponseMessage delete = await HttpClient.DeleteAsync($"todos/{missingId}");
        HttpResponseMessage copy = await HttpClient.PostAsJsonAsync($"todos/{missingId}/copy", new { userId });

        // Assert
        update.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        copy.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CopyTodo_Should_ReturnNotFound_WhenIdIsNotAGuid()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("todos/abc/copy", new { userId });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CompleteTodo_Should_MarkTodoAsCompleted()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        var createRequest = new
        {
            userId,
            description = "Todo to complete",
            labels = Array.Empty<string>(),
            priority = 1
        };
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("todos", createRequest);
        createResponse.EnsureSuccessStatusCode();
        Guid todoId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        HttpResponseMessage completeResponse = await HttpClient.PutAsync($"todos/{todoId}/complete", null);

        // Assert
        completeResponse.EnsureSuccessStatusCode();

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"todos/{todoId}");
        getResponse.EnsureSuccessStatusCode();
        TodoDto? todo = await getResponse.Content.ReadFromJsonAsync<TodoDto>();
        todo!.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateTodo_Should_ReturnConflict_WhenIdempotencyKeyIsReused()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        var createRequest = new
        {
            userId,
            description = "Idempotent todo",
            labels = Array.Empty<string>(),
            priority = 1
        };
        string idempotencyKey = Guid.NewGuid().ToString();

        // Act
        HttpResponseMessage first = await PostTodoAsync(createRequest, idempotencyKey);
        HttpResponseMessage second = await PostTodoAsync(createRequest, idempotencyKey);

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateTodo_Should_Succeed_WhenSameIdempotencyKeyIsUsedByDifferentUsers()
    {
        // Arrange
        (Guid firstUserId, AccessTokens firstTokens) = await RegisterAndLoginAsync();
        (Guid secondUserId, AccessTokens secondTokens) = await RegisterAndLoginAsync();
        string idempotencyKey = Guid.NewGuid().ToString();

        // Act
        Authenticate(firstTokens.AccessToken);
        HttpResponseMessage first = await PostTodoAsync(
            new { userId = firstUserId, description = "First user todo", labels = Array.Empty<string>(), priority = 1 },
            idempotencyKey);

        Authenticate(secondTokens.AccessToken);
        HttpResponseMessage second = await PostTodoAsync(
            new { userId = secondUserId, description = "Second user todo", labels = Array.Empty<string>(), priority = 1 },
            idempotencyKey);

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateTodo_Should_Succeed_WhenIdempotencyKeyIsRetriedAfterValidationError()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        string idempotencyKey = Guid.NewGuid().ToString();

        // Act
        HttpResponseMessage invalid = await PostTodoAsync(
            new { userId, description = string.Empty, labels = Array.Empty<string>(), priority = 1 },
            idempotencyKey);
        HttpResponseMessage retry = await PostTodoAsync(
            new { userId, description = "Retried todo", labels = Array.Empty<string>(), priority = 1 },
            idempotencyKey);

        // Assert
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetTodos_Should_ReturnPagedResults()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        var createRequest = new
        {
            userId,
            description = "Paged todo",
            labels = Array.Empty<string>(),
            priority = 1
        };
        await PostTodoAsync(createRequest, Guid.NewGuid().ToString());
        await PostTodoAsync(createRequest, Guid.NewGuid().ToString());

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"todos?userId={userId}&page=1&pageSize=1");

        // Assert
        response.EnsureSuccessStatusCode();
        PagedTodos? page = await response.Content.ReadFromJsonAsync<PagedTodos>();
        page!.Items.Count.ShouldBe(1);
        page.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
        page.HasNextPage.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1, 1000)]
    [InlineData(0, 10)]
    public async Task GetTodos_Should_ReturnBadRequest_WhenPaginationIsOutOfRange(int page, int pageSize)
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"todos?userId={userId}&page={page}&pageSize={pageSize}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Validation.General");
    }

    [Fact]
    public async Task GetTodos_Should_MatchSearch_IgnoringCase()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync(
            "todos",
            new { userId, description = "Comprar Leite", labels = Array.Empty<string>(), priority = 1 });
        createResponse.EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"todos?userId={userId}&search=leite");

        // Assert
        response.EnsureSuccessStatusCode();
        PagedTodos? page = await response.Content.ReadFromJsonAsync<PagedTodos>();
        page!.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Description.ShouldBe("Comprar Leite");
    }

    [Fact]
    public async Task GetTodos_Should_TreatLikeWildcardsInSearchAsLiteralText()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        foreach (string description in new[] { "100% done", "100 done", "a_b", "axb" })
        {
            HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync(
                "todos",
                new { userId, description, labels = Array.Empty<string>(), priority = 1 });
            createResponse.EnsureSuccessStatusCode();
        }

        // Act
        HttpResponseMessage percentResponse = await HttpClient.GetAsync($"todos?userId={userId}&search={Uri.EscapeDataString("%")}");
        HttpResponseMessage underscoreResponse = await HttpClient.GetAsync($"todos?userId={userId}&search=a_b");

        // Assert
        percentResponse.EnsureSuccessStatusCode();
        PagedTodos? percentPage = await percentResponse.Content.ReadFromJsonAsync<PagedTodos>();
        percentPage!.Items.ShouldHaveSingleItem().Description.ShouldBe("100% done");

        underscoreResponse.EnsureSuccessStatusCode();
        PagedTodos? underscorePage = await underscoreResponse.Content.ReadFromJsonAsync<PagedTodos>();
        underscorePage!.Items.ShouldHaveSingleItem().Description.ShouldBe("a_b");
    }

    [Fact]
    public async Task GetTodos_Should_ReturnForbidden_WhenUserIdIsNotCurrentUser()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"todos?userId={Guid.NewGuid()}&page=1&pageSize=10");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.Unauthorized");
    }

    [Fact]
    public async Task CreateTodo_Should_ReturnForbidden_WhenUserIdIsNotCurrentUser()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        var createRequest = new
        {
            userId = Guid.NewGuid(),
            description = "Someone else's todo",
            labels = Array.Empty<string>(),
            priority = 1
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("todos", createRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.Unauthorized");
    }

    private sealed record ProblemDto(string Title, int Status);

    private sealed record PagedTodos(
        List<TodoDto> Items,
        int Page,
        int PageSize,
        int TotalCount,
        bool HasNextPage,
        bool HasPreviousPage);

    private async Task<Guid> CreateTodoAsync(Guid userId, string description)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "todos",
            new { userId, description, labels = Array.Empty<string>(), priority = 1 });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<HttpResponseMessage> PostTodoAsync(object request, string idempotencyKey)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "todos")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);

        return await HttpClient.SendAsync(message);
    }
}
