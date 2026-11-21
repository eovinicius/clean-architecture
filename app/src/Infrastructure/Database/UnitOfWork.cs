using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database;

internal sealed class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException("Another request changed the same data first.", exception);
        }
    }
}
