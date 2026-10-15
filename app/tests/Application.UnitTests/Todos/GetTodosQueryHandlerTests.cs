using Application.Abstractions.Authentication;
using Application.Abstractions.Pagination;
using Application.Todos.Get;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class GetTodosQueryHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnMatchingTodos_WhenSearchCaseDiffers()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.TodoItems.AddRange(
            CreateTodo("Comprar Leite"),
            CreateTodo("Pagar contas"));
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);

        var handler = new GetTodosQueryHandler(context, userContext);
        var query = new GetTodosQuery(UserId, Search: "leite", SortColumn: null, SortOrder: null, Page: 1, PageSize: 10);

        // Act
        Result<PagedList<TodoResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(1);
        result.Value.Items.ShouldHaveSingleItem().Description.ShouldBe("Comprar Leite");
    }

    private static TodoItem CreateTodo(string description) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Description = description,
            Priority = Priority.Normal,
            Labels = [],
            CreatedAt = DateTime.UtcNow
        };
}
