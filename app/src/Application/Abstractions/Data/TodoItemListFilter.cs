namespace Application.Abstractions.Data;

public sealed record TodoItemListFilter(
    Guid UserId,
    bool IncludeArchived,
    string? Search,
    string? SortColumn,
    string? SortOrder,
    int Page,
    int PageSize);
