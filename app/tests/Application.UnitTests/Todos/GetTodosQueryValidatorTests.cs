using Application.Todos.Get;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Todos;

public sealed class GetTodosQueryValidatorTests
{
    private readonly GetTodosQueryValidator _validator = new();

    [Fact]
    public void Validator_Should_HaveError_WhenPageIsZero()
    {
        // Arrange
        GetTodosQuery query = CreateQuery(page: 0, pageSize: 10);

        // Act
        TestValidationResult<GetTodosQuery> result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(q => q.Page);
    }

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

    [Fact]
    public void Validator_Should_NotHaveErrors_WhenPaginationIsAtTheLimits()
    {
        // Arrange
        GetTodosQuery query = CreateQuery(page: 1, pageSize: 100);

        // Act
        TestValidationResult<GetTodosQuery> result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static GetTodosQuery CreateQuery(int page, int pageSize) =>
        new(Guid.NewGuid(), Search: null, SortColumn: null, SortOrder: null, page, pageSize, IncludeArchived: false);
}
