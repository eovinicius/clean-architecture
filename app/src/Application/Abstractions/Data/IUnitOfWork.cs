namespace Application.Abstractions.Data;

public interface IUnitOfWork
{
    // Writes every change tracked in the request scope atomically.
    // Throws ConcurrencyConflictException when another request changed the same row first.
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
