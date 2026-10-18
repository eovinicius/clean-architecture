using FluentValidation;

namespace Application.Todos.Archive;

internal sealed class ArchiveTodoCommandValidator : AbstractValidator<ArchiveTodoCommand>
{
    public ArchiveTodoCommandValidator()
    {
        RuleFor(c => c.TodoItemId).NotEmpty();
    }
}
