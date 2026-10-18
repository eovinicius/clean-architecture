using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Todos;

public sealed class TodoArchivingTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record TodoDto(Guid Id, string Description, bool IsCompleted, bool IsArchived, DateTime? ArchivedAt);

    private sealed record ProblemDto(string Title, int Status);

    private sealed record PagedTodos(List<TodoDto> Items, int TotalCount);

    [Fact] // SPEC-0001 AC-1, AC-9
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

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"todos/{todoId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        TodoDto? todo = await getResponse.Content.ReadFromJsonAsync<TodoDto>();
        todo!.IsArchived.ShouldBeTrue();
        todo.ArchivedAt.ShouldNotBeNull();
    }

    [Fact] // SPEC-0001 AC-2
    public async Task ArchiveTodo_Should_ReturnBadRequest_WhenTodoIsNotCompleted()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid todoId = await CreateTodoAsync(userId, "Not completed todo");

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"todos/{todoId}/archive", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("TodoItems.NotCompleted");

        TodoDto? todo = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");
        todo!.IsArchived.ShouldBeFalse();
    }

    [Fact] // SPEC-0001 AC-3
    public async Task ArchiveTodo_Should_BeIdempotent_WhenTodoIsAlreadyArchived()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid todoId = await CreateArchivedTodoAsync(userId);
        TodoDto? archived = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"todos/{todoId}/archive", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        TodoDto? todo = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");
        todo!.IsArchived.ShouldBeTrue();
        todo.ArchivedAt.ShouldBe(archived!.ArchivedAt);
    }

    [Fact] // SPEC-0001 AC-4
    public async Task UnarchiveTodo_Should_UnarchiveArchivedTodo()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid todoId = await CreateArchivedTodoAsync(userId);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"todos/{todoId}/unarchive", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        TodoDto? todo = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");
        todo!.IsArchived.ShouldBeFalse();
        todo.ArchivedAt.ShouldBeNull();
        todo.IsCompleted.ShouldBeTrue();
    }

    [Fact] // SPEC-0001 AC-5
    public async Task UnarchiveTodo_Should_ReturnNoContent_WhenTodoIsNotArchived()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid todoId = await CreateTodoAsync(userId, "Not archived todo");

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"todos/{todoId}/unarchive", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        TodoDto? todo = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");
        todo!.IsArchived.ShouldBeFalse();
    }

    [Fact] // SPEC-0001 AC-6
    public async Task UpdateTodo_Should_ReturnBadRequest_WhenTodoIsArchived()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid todoId = await CreateArchivedTodoAsync(userId, "Original description");

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"todos/{todoId}", new { description = "Updated description" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("TodoItems.Archived");

        TodoDto? todo = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");
        todo!.Description.ShouldBe("Original description");
    }

    [Fact] // SPEC-0001 AC-7
    public async Task CompleteTodo_Should_ReturnBadRequest_WhenTodoIsArchived()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid todoId = await CreateArchivedTodoAsync(userId);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"todos/{todoId}/complete", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("TodoItems.Archived");
    }

    [Fact] // SPEC-0001 AC-8
    public async Task DeleteTodo_Should_RemoveArchivedTodo()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid todoId = await CreateArchivedTodoAsync(userId);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"todos/{todoId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        HttpResponseMessage getResponse = await HttpClient.GetAsync($"todos/{todoId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await getResponse.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("TodoItems.NotFound");
    }

    [Fact] // SPEC-0001 AC-10
    public async Task GetTodos_Should_OmitArchivedTodos_ByDefault()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid activeId = await CreateTodoAsync(userId, "Active todo");
        await CreateArchivedTodoAsync(userId);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"todos?userId={userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedTodos? page = await response.Content.ReadFromJsonAsync<PagedTodos>();
        page!.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(activeId);
    }

    [Fact] // SPEC-0001 AC-11
    public async Task GetTodos_Should_IncludeArchivedTodos_WhenIncludeArchivedIsTrue()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid activeId = await CreateTodoAsync(userId, "Active todo");
        Guid archivedId = await CreateArchivedTodoAsync(userId);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"todos?userId={userId}&includeArchived=true");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedTodos? page = await response.Content.ReadFromJsonAsync<PagedTodos>();
        page!.TotalCount.ShouldBe(2);
        page.Items.Single(t => t.Id == activeId).IsArchived.ShouldBeFalse();
        page.Items.Single(t => t.Id == archivedId).IsArchived.ShouldBeTrue();
    }

    [Fact] // SPEC-0001 AC-12
    public async Task GetTodoById_Should_ReflectArchiveState_AfterTodoWasCached()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid todoId = await CreateCompletedTodoAsync(userId);
        TodoDto? cached = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");
        cached!.IsArchived.ShouldBeFalse();

        // Act
        (await HttpClient.PutAsync($"todos/{todoId}/archive", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        TodoDto? afterArchive = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");

        (await HttpClient.PutAsync($"todos/{todoId}/unarchive", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        TodoDto? afterUnarchive = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");

        // Assert
        afterArchive!.IsArchived.ShouldBeTrue();
        afterArchive.ArchivedAt.ShouldNotBeNull();
        afterUnarchive!.IsArchived.ShouldBeFalse();
        afterUnarchive.ArchivedAt.ShouldBeNull();
    }

    [Theory] // SPEC-0001 AC-13
    [InlineData("archive")]
    [InlineData("unarchive")]
    public async Task ArchiveEndpoints_Should_ReturnUnauthorized_WhenTokenIsMissing(string action)
    {
        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"todos/{Guid.NewGuid()}/{action}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory] // SPEC-0001 AC-14
    [InlineData("archive")]
    [InlineData("unarchive")]
    public async Task ArchiveEndpoints_Should_ReturnNotFound_WhenTodoBelongsToAnotherUser(string action)
    {
        // Arrange
        (Guid ownerId, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid todoId = action == "archive"
            ? await CreateCompletedTodoAsync(ownerId)
            : await CreateArchivedTodoAsync(ownerId);
        TodoDto? before = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");

        (_, AccessTokens otherTokens) = await RegisterAndLoginAsync();
        Authenticate(otherTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"todos/{todoId}/{action}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("TodoItems.NotFound");

        Authenticate(ownerTokens.AccessToken);
        TodoDto? after = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");
        after!.IsArchived.ShouldBe(before!.IsArchived);
    }

    [Theory] // SPEC-0001 AC-14
    [InlineData("archive")]
    [InlineData("unarchive")]
    public async Task ArchiveEndpoints_Should_ReturnNotFound_WhenTodoDoesNotExist(string action)
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"todos/{Guid.NewGuid()}/{action}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory] // SPEC-0001 AC-15
    [InlineData("archive")]
    [InlineData("unarchive")]
    public async Task ArchiveEndpoints_Should_ReturnBadRequest_WhenIdIsEmpty(string action)
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"todos/{Guid.Empty}/{action}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Validation.General");
    }

    [Fact] // SPEC-0001 AC-16
    public async Task CopyTodo_Should_CreateNotArchivedCopy_WhenTodoIsArchived()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid todoId = await CreateArchivedTodoAsync(userId);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync($"todos/{todoId}/copy", new { userId });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Guid copyId = await response.Content.ReadFromJsonAsync<Guid>();

        TodoDto? copy = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{copyId}");
        copy!.IsArchived.ShouldBeFalse();
        copy.ArchivedAt.ShouldBeNull();
        copy.IsCompleted.ShouldBeFalse();
    }

    private async Task<Guid> CreateTodoAsync(Guid userId, string description)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "todos",
            new { userId, description, labels = Array.Empty<string>(), priority = 1 });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<Guid> CreateCompletedTodoAsync(Guid userId, string description = "Completed todo")
    {
        Guid todoId = await CreateTodoAsync(userId, description);

        HttpResponseMessage response = await HttpClient.PutAsync($"todos/{todoId}/complete", null);
        response.EnsureSuccessStatusCode();

        return todoId;
    }

    private async Task<Guid> CreateArchivedTodoAsync(Guid userId, string description = "Archived todo")
    {
        Guid todoId = await CreateCompletedTodoAsync(userId, description);

        HttpResponseMessage response = await HttpClient.PutAsync($"todos/{todoId}/archive", null);
        response.EnsureSuccessStatusCode();

        return todoId;
    }
}
