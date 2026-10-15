using FluentValidation;
using SharedKernel;

namespace Application.Todos.Create;

public class CreateTodoCommandValidator : AbstractValidator<CreateTodoCommand>
{
    public CreateTodoCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Priority).IsInEnum();
        RuleFor(c => c.Description).NotEmpty().MaximumLength(500);

        // One day of slack so users in time zones behind UTC can still pick their local "today".
        RuleFor(c => c.DueDate)
            .GreaterThanOrEqualTo(_ => dateTimeProvider.UtcNow.Date.AddDays(-1))
            .When(x => x.DueDate.HasValue);
    }
}
