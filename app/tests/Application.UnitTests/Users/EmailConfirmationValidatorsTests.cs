using Application.Users.ConfirmEmail;
using Application.Users.ResendEmailConfirmation;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Users;

public sealed class EmailConfirmationValidatorsTests
{
    private readonly ConfirmEmailCommandValidator _confirmValidator = new();
    private readonly ResendEmailConfirmationCommandValidator _resendValidator = new();

    [Theory] // SPEC-0003 AC-8a
    [InlineData("")]
    [InlineData(" ")]
    public void ConfirmValidator_Should_HaveError_WhenTokenIsEmpty(string token)
    {
        // Act
        TestValidationResult<ConfirmEmailCommand> result = _confirmValidator.TestValidate(new ConfirmEmailCommand(token));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Token);
    }

    [Theory] // SPEC-0003 AC-8a
    [InlineData(128, false)]
    [InlineData(129, true)]
    public void ConfirmValidator_Should_LimitTokenLength(int length, bool hasError)
    {
        // Act
        TestValidationResult<ConfirmEmailCommand> result =
            _confirmValidator.TestValidate(new ConfirmEmailCommand(new string('a', length)));

        // Assert
        if (hasError)
        {
            result.ShouldHaveValidationErrorFor(c => c.Token);
        }
        else
        {
            result.ShouldNotHaveAnyValidationErrors();
        }
    }

    [Theory] // SPEC-0003 AC-14
    [InlineData("")]
    [InlineData("not-an-email")]
    public void ResendValidator_Should_HaveError_WhenEmailIsInvalid(string email)
    {
        // Act
        TestValidationResult<ResendEmailConfirmationCommand> result =
            _resendValidator.TestValidate(new ResendEmailConfirmationCommand(email));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact] // SPEC-0003 AC-14
    public void ResendValidator_Should_HaveError_WhenEmailExceedsMaxLength()
    {
        // Arrange
        string email = $"{new string('a', 309)}@example.com";

        // Act
        TestValidationResult<ResendEmailConfirmationCommand> result =
            _resendValidator.TestValidate(new ResendEmailConfirmationCommand(email));

        // Assert
        email.Length.ShouldBe(321);
        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void ResendValidator_Should_NotHaveErrors_WhenEmailIsValid()
    {
        // Act
        TestValidationResult<ResendEmailConfirmationCommand> result =
            _resendValidator.TestValidate(new ResendEmailConfirmationCommand("test@example.com"));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
