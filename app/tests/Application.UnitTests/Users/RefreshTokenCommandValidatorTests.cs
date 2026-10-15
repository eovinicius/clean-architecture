using Application.Users.Refresh;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Users;

public sealed class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _validator = new();

    [Fact]
    public void Validator_Should_HaveError_WhenRefreshTokenIsEmpty()
    {
        // Arrange
        var command = new RefreshTokenCommand(string.Empty);

        // Act
        TestValidationResult<RefreshTokenCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.RefreshToken);
    }

    [Fact]
    public void Validator_Should_NotHaveErrors_WhenRefreshTokenIsPresent()
    {
        // Arrange
        var command = new RefreshTokenCommand("refresh-token");

        // Act
        TestValidationResult<RefreshTokenCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
