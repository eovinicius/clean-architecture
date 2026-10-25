using FluentValidation;

namespace Application.Users.RequestPasswordReset;

internal sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(320).EmailAddress();
    }
}
