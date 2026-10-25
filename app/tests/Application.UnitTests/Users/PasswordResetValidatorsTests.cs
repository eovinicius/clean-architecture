using Application.Users.RequestPasswordReset;
using Application.Users.ResetPassword;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Users;

public sealed class PasswordResetValidatorsTests
{
    private const string ValidToken = "token";
    private const string ValidPassword = "Password123";

    private readonly RequestPasswordResetCommandValidator _requestValidator = new();
    private readonly ResetPasswordCommandValidator _resetValidator = new();

    [Theory] // SPEC-0004 AC-4a
    [InlineData("")]
    [InlineData("not-an-email")]
    public void RequestValidator_Should_HaveError_WhenEmailIsInvalid(string email)
    {
        // Act
        TestValidationResult<RequestPasswordResetCommand> result =
            _requestValidator.TestValidate(new RequestPasswordResetCommand(email));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact] // SPEC-0004 AC-4a
    public void RequestValidator_Should_HaveError_WhenEmailExceedsMaxLength()
    {
        // Arrange
        string email = $"{new string('a', 309)}@example.com";

        // Act
        TestValidationResult<RequestPasswordResetCommand> result =
            _requestValidator.TestValidate(new RequestPasswordResetCommand(email));

        // Assert
        email.Length.ShouldBe(321);
        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Theory] // SPEC-0004 AC-8
    [InlineData("")]
    [InlineData(" ")]
    public void ResetValidator_Should_HaveError_WhenTokenIsEmpty(string token)
    {
        // Act
        TestValidationResult<ResetPasswordCommand> result =
            _resetValidator.TestValidate(new ResetPasswordCommand(token, ValidPassword));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Token);
    }

    [Fact] // SPEC-0004 AC-8
    public void ResetValidator_Should_HaveError_WhenTokenExceedsMaxLength()
    {
        // Act
        TestValidationResult<ResetPasswordCommand> result =
            _resetValidator.TestValidate(new ResetPasswordCommand(new string('a', 129), ValidPassword));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Token);
    }

    [Theory] // SPEC-0004 AC-8 (RN-9)
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(8, false)]
    [InlineData(128, false)]
    [InlineData(129, true)]
    public void ResetValidator_Should_LimitNewPasswordLength(int length, bool hasError)
    {
        // Act
        TestValidationResult<ResetPasswordCommand> result =
            _resetValidator.TestValidate(new ResetPasswordCommand(ValidToken, new string('a', length)));

        // Assert
        if (hasError)
        {
            result.ShouldHaveValidationErrorFor(c => c.NewPassword);
        }
        else
        {
            result.ShouldNotHaveAnyValidationErrors();
        }
    }

    [Fact] // SPEC-0004 seção 7 (a senha não aparece no erro)
    public void ResetValidator_Should_NotEchoTheNewPassword_InErrorMessages()
    {
        // Arrange
        string password = new('s', 129);

        // Act
        TestValidationResult<ResetPasswordCommand> result =
            _resetValidator.TestValidate(new ResetPasswordCommand(ValidToken, password));

        // Assert
        result.Errors.ShouldNotBeEmpty();
        result.Errors.ShouldAllBe(error => !error.ErrorMessage.Contains(password, StringComparison.Ordinal));
    }
}
