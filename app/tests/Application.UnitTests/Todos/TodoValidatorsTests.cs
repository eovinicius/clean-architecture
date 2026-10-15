using Application.Todos.Complete;
using Application.Todos.Copy;
using Application.Todos.Create;
using Application.Todos.Delete;
using Application.Todos.Update;
using Domain.Todos;
using FluentValidation.TestHelper;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class TodoValidatorsTests
{
    private static readonly DateTime UtcNow = new(2026, 10, 15, 1, 0, 0, DateTimeKind.Utc);

    private readonly CreateTodoCommandValidator _createValidator = new(CreateDateTimeProvider());
    private readonly UpdateTodoCommandValidator _updateValidator = new();
    private readonly CompleteTodoCommandValidator _completeValidator = new();
    private readonly DeleteTodoCommandValidator _deleteValidator = new();
    private readonly CopyTodoCommandValidator _copyValidator = new();

    [Fact]
    public void CompleteValidator_Should_HaveError_WhenTodoItemIdIsEmpty()
    {
        // Act
        TestValidationResult<CompleteTodoCommand> result = _completeValidator.TestValidate(new CompleteTodoCommand(Guid.Empty));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.TodoItemId);
    }

    [Fact]
    public void CompleteValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Act
        TestValidationResult<CompleteTodoCommand> result = _completeValidator.TestValidate(new CompleteTodoCommand(Guid.NewGuid()));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeleteValidator_Should_HaveError_WhenTodoItemIdIsEmpty()
    {
        // Act
        TestValidationResult<DeleteTodoCommand> result = _deleteValidator.TestValidate(new DeleteTodoCommand(Guid.Empty));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.TodoItemId);
    }

    [Fact]
    public void DeleteValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Act
        TestValidationResult<DeleteTodoCommand> result = _deleteValidator.TestValidate(new DeleteTodoCommand(Guid.NewGuid()));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CopyValidator_Should_HaveErrors_WhenIdsAreEmpty()
    {
        // Arrange
        var command = new CopyTodoCommand { UserId = Guid.Empty, TodoId = Guid.Empty };

        // Act
        TestValidationResult<CopyTodoCommand> result = _copyValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserId);
        result.ShouldHaveValidationErrorFor(c => c.TodoId);
    }

    [Fact]
    public void CopyValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new CopyTodoCommand { UserId = Guid.NewGuid(), TodoId = Guid.NewGuid() };

        // Act
        TestValidationResult<CopyTodoCommand> result = _copyValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(500, false)]
    [InlineData(501, true)]
    public void CreateValidator_Should_EnforceDescriptionMaxLength(int length, bool shouldHaveError)
    {
        // Arrange
        var command = new CreateTodoCommand
        {
            UserId = Guid.NewGuid(),
            Description = new string('a', length),
            Priority = Priority.Low
        };

        // Act
        TestValidationResult<CreateTodoCommand> result = _createValidator.TestValidate(command);

        // Assert
        if (shouldHaveError)
        {
            result.ShouldHaveValidationErrorFor(c => c.Description);
        }
        else
        {
            result.ShouldNotHaveValidationErrorFor(c => c.Description);
        }
    }

    [Fact]
    public void CreateValidator_Should_NotHaveError_WhenDueDateIsWithinOneDayOfUtcToday()
    {
        // Arrange
        var command = new CreateTodoCommand
        {
            UserId = Guid.NewGuid(),
            Description = "Due yesterday in UTC, today locally",
            Priority = Priority.Low,
            DueDate = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Unspecified)
        };

        // Act
        TestValidationResult<CreateTodoCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.DueDate);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDueDateIsBeforeTheSlack()
    {
        // Arrange
        var command = new CreateTodoCommand
        {
            UserId = Guid.NewGuid(),
            Description = "Due two days ago",
            Priority = Priority.Low,
            DueDate = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Unspecified)
        };

        // Act
        TestValidationResult<CreateTodoCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.DueDate);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDescriptionIsEmpty()
    {
        var command = new CreateTodoCommand
        {
            UserId = Guid.NewGuid(),
            Description = string.Empty,
            Priority = Priority.Low
        };

        TestValidationResult<CreateTodoCommand> result = _createValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void CreateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new CreateTodoCommand
        {
            UserId = Guid.NewGuid(),
            Description = "Buy groceries",
            Priority = Priority.Medium,
            Labels = ["home"]
        };

        TestValidationResult<CreateTodoCommand> result = _createValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenDescriptionExceedsMaxLength()
    {
        var command = new UpdateTodoCommand(Guid.NewGuid(), new string('a', 501));

        TestValidationResult<UpdateTodoCommand> result = _updateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void UpdateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new UpdateTodoCommand(Guid.NewGuid(), "Updated description");

        TestValidationResult<UpdateTodoCommand> result = _updateValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static IDateTimeProvider CreateDateTimeProvider()
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(UtcNow);

        return dateTimeProvider;
    }
}
