namespace Application.Abstractions.Pagination;

public sealed class PagedList<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public bool HasNextPage => Page * PageSize < TotalCount;

    public bool HasPreviousPage => Page > 1;

    public PagedList<TOut> Map<TOut>(Func<T, TOut> selector) => new()
    {
        Items = [.. Items.Select(selector)],
        Page = Page,
        PageSize = PageSize,
        TotalCount = TotalCount
    };
}
