using Application.Users.Register;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Users;

public sealed class RegisterUserCommandValidatorTests
{
    private const string ValidEmail = "test@example.com";
    private const string ValidName = "Test";
    private const string ValidPassword = "Password123";

    private readonly RegisterUserCommandValidator _validator = new();

    [Fact]
    public void Validator_Should_HaveError_WhenEmailExceedsMaxLength()
    {
        // Arrange
        string email = $"{new string('a', 309)}@example.com";
        var command = new RegisterUserCommand(email, ValidName, ValidName, ValidPassword);

        // Act
        TestValidationResult<RegisterUserCommand> result = _validator.TestValidate(command);

        // Assert
        email.Length.ShouldBe(321);
        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void Validator_Should_HaveError_WhenFirstNameExceedsMaxLength()
    {
        // Arrange
        var command = new RegisterUserCommand(ValidEmail, new string('a', 101), ValidName, ValidPassword);

        // Act
        TestValidationResult<RegisterUserCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.FirstName);
    }

    [Fact]
    public void Validator_Should_HaveError_WhenLastNameExceedsMaxLength()
    {
        // Arrange
        var command = new RegisterUserCommand(ValidEmail, ValidName, new string('a', 101), ValidPassword);

        // Act
        TestValidationResult<RegisterUserCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.LastName);
    }

    [Fact]
    public void Validator_Should_NotHaveErrors_WhenFieldsAreAtMaxLength()
    {
        // Arrange
        string email = $"{new string('a', 308)}@example.com";
        var command = new RegisterUserCommand(email, new string('a', 100), new string('a', 100), ValidPassword);

        // Act
        TestValidationResult<RegisterUserCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
