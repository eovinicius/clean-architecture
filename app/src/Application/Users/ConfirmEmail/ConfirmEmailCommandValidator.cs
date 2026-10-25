using FluentValidation;

namespace Application.Users.ConfirmEmail;

internal sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty().MaximumLength(128);
    }
}
