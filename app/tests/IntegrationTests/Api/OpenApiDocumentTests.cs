using System.Text.Json;

namespace IntegrationTests.Api;

// Guards the generated OpenAPI document (what Scalar renders) against drifting from the real contract.
public sealed class OpenApiDocumentTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Theory]
    [InlineData("/users/register", "email", "firstName", "lastName", "password")]
    [InlineData("/users/login", "email", "password")]
    [InlineData("/users/refresh-token", "refreshToken")]
    [InlineData("/users/confirm-email", "token")] // SPEC-0003 AC-17
    [InlineData("/users/resend-email-confirmation", "email")] // SPEC-0003 AC-17
    [InlineData("/users/forgot-password", "email")] // SPEC-0004 AC-13
    [InlineData("/users/reset-password", "token", "newPassword")] // SPEC-0004 AC-13
    [InlineData("/todos", "userId", "description", "dueDate", "labels", "priority")]
    [InlineData("/todos/{todoId}/copy", "userId")]
    public async Task RequestBody_Should_MatchEndpointRequest(string path, params string[] expectedProperties)
    {
        // Arrange
        using JsonDocument document = await GetDocumentAsync();
        JsonElement root = document.RootElement;

        // Act
        JsonElement schema = Resolve(root, root
            .GetProperty("paths").GetProperty(path).GetProperty("post")
            .GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema"));

        // Assert
        string[] properties = [.. schema.GetProperty("properties").EnumerateObject().Select(p => p.Name)];
        properties.ShouldBe(expectedProperties, ignoreOrder: true);
    }

    [Fact]
    public async Task CreateTodo_Should_DocumentPriorityValues()
    {
        // Arrange
        using JsonDocument document = await GetDocumentAsync();
        JsonElement root = document.RootElement;

        // Act
        JsonElement priority = Resolve(root, root
            .GetProperty("components").GetProperty("schemas")
            .GetProperty("TodosCreateRequest").GetProperty("properties").GetProperty("priority"));

        // Assert
        priority.GetProperty("enum").GetArrayLength().ShouldBe(5);
    }

    [Theory]
    [InlineData("/users/register", "post", false)]
    [InlineData("/users/login", "post", false)]
    [InlineData("/todos", "post", true)]
    [InlineData("/todos/{id}", "get", true)]
    [InlineData("/todos/{id}/archive", "put", true)] // SPEC-0001
    [InlineData("/todos/{id}/unarchive", "put", true)] // SPEC-0001
    [InlineData("/users/me", "get", true)] // SPEC-0002 AC-9
    [InlineData("/users/confirm-email", "post", false)] // SPEC-0003 AC-17
    [InlineData("/users/resend-email-confirmation", "post", false)] // SPEC-0003 AC-17
    [InlineData("/users/forgot-password", "post", false)] // SPEC-0004 AC-13
    [InlineData("/users/reset-password", "post", false)] // SPEC-0004 AC-13
    public async Task Operation_Should_RequireBearerOnlyWhenProtected(string path, string method, bool isProtected)
    {
        // Arrange
        using JsonDocument document = await GetDocumentAsync();
        JsonElement root = document.RootElement;

        // Act
        JsonElement operation = root.GetProperty("paths").GetProperty(path).GetProperty(method);

        // Assert
        root.TryGetProperty("security", out _).ShouldBeFalse();
        operation.TryGetProperty("security", out _).ShouldBe(isProtected);
        operation.GetProperty("responses").TryGetProperty("401", out _).ShouldBe(isProtected || path == "/users/login");
    }

    [Theory] // SPEC-0003 AC-17
    [InlineData("/users/confirm-email", "204", "400")]
    [InlineData("/users/resend-email-confirmation", "202", "400")]
    [InlineData("/users/login", "200", "403")]
    [InlineData("/users/forgot-password", "202", "400")] // SPEC-0004 AC-13
    [InlineData("/users/reset-password", "204", "400")] // SPEC-0004 AC-13
    public async Task AccountOperations_Should_DocumentResponses(string path, params string[] expectedStatuses)
    {
        // Arrange
        using JsonDocument document = await GetDocumentAsync();

        // Act
        JsonElement responses = document.RootElement
            .GetProperty("paths").GetProperty(path).GetProperty("post").GetProperty("responses");

        // Assert
        foreach (string status in expectedStatuses)
        {
            responses.TryGetProperty(status, out _).ShouldBeTrue($"{path} should document {status}");
        }
    }

    [Fact]
    public async Task CreateTodo_Should_DocumentIdempotencyKeyHeader()
    {
        // Arrange
        using JsonDocument document = await GetDocumentAsync();

        // Act
        JsonElement operation = document.RootElement.GetProperty("paths").GetProperty("/todos").GetProperty("post");

        // Assert
        operation.GetProperty("parameters").EnumerateArray()
            .ShouldContain(p => p.GetProperty("name").GetString() == "Idempotency-Key" &&
                                p.GetProperty("in").GetString() == "header");
        operation.GetProperty("responses").TryGetProperty("409", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task GetTodoById_Should_DocumentResponses()
    {
        // Arrange
        using JsonDocument document = await GetDocumentAsync();
        JsonElement root = document.RootElement;

        // Act
        JsonElement responses = root.GetProperty("paths").GetProperty("/todos/{id}").GetProperty("get").GetProperty("responses");

        // Assert
        JsonElement schema = responses.GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        schema.GetProperty("$ref").GetString().ShouldBe("#/components/schemas/TodoResponse");
        responses.TryGetProperty("404", out _).ShouldBeTrue();
    }

    private async Task<JsonDocument> GetDocumentAsync()
    {
        await using Stream stream = await HttpClient.GetStreamAsync("openapi/v1.json");

        return await JsonDocument.ParseAsync(stream);
    }

    // Follows a local "$ref" ("#/components/schemas/X") to the schema it points at.
    private static JsonElement Resolve(JsonElement root, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out JsonElement reference))
        {
            return schema;
        }

        string name = reference.GetString()!.Split('/')[^1];

        return root.GetProperty("components").GetProperty("schemas").GetProperty(name);
    }
}
