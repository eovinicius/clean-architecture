using FluentValidation;

namespace Application.Todos.Unarchive;

internal sealed class UnarchiveTodoCommandValidator : AbstractValidator<UnarchiveTodoCommand>
{
    public UnarchiveTodoCommandValidator()
    {
        RuleFor(c => c.TodoItemId).NotEmpty();
    }
}
