using FluentValidation;

namespace Application.Todos.Get;

internal sealed class GetTodosQueryValidator : AbstractValidator<GetTodosQuery>
{
    private const int MaxPageSize = 100;

    public GetTodosQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);

        RuleFor(q => q.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}
