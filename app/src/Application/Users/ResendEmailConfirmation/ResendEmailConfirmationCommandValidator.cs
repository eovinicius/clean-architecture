using FluentValidation;

namespace Application.Users.ResendEmailConfirmation;

internal sealed class ResendEmailConfirmationCommandValidator : AbstractValidator<ResendEmailConfirmationCommand>
{
    public ResendEmailConfirmationCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(320).EmailAddress();
    }
}
