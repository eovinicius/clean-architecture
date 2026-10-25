using FluentValidation;

namespace Application.Users.ResetPassword;

internal sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty().MaximumLength(128);

        // SPEC-0004 RN-9: mesmo mínimo do cadastro, com teto para não sobrecarregar o hasher.
        RuleFor(c => c.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}
