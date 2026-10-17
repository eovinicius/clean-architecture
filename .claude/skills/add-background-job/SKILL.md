---
name: add-background-job
description: Add a background job (a hosted BackgroundService) to the Clean Architecture template that runs on a schedule and safely resolves scoped services. Use when the user wants a recurring task, worker, cleanup job, or scheduled processing.
argument-hint: <what the job does, e.g. "delete expired refresh tokens every hour">
---

# Add a Background Job

Recurring work runs as a `BackgroundService` in the **Web.Api** project. A hosted service is a **singleton**, so scoped services (DbContext, handlers) must be resolved inside a scope created per iteration.

The job is only a scheduler: the work itself is a **command** in the Application layer (business rule + `IDateTimeProvider`), so it stays unit-testable and Web.Api keeps no business logic.

## Files to create/modify

1. **The use case** — a command slice in `app/src/Application/{Feature}/{UseCase}/` (see the `add-feature` skill for the full conventions and tests):

```csharp
using Application.Abstractions.Messaging;

namespace Application.Users.DeleteExpiredRefreshTokens;

public sealed record DeleteExpiredRefreshTokensCommand : ICommand;
```

```csharp
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.DeleteExpiredRefreshTokens;

internal sealed class DeleteExpiredRefreshTokensCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteExpiredRefreshTokensCommand>
{
    public async Task<Result> Handle(DeleteExpiredRefreshTokensCommand command, CancellationToken cancellationToken)
    {
        await context.RefreshTokens
            .Where(token => token.ExpiresOnUtc < dateTimeProvider.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);

        return Result.Success();
    }
}
```

2. **The job** — `app/src/Web.Api/BackgroundJobs/{Name}BackgroundService.cs`

```csharp
using Application.Abstractions.Messaging;
using Application.Users.DeleteExpiredRefreshTokens;
using SharedKernel;

namespace Web.Api.BackgroundJobs;

internal sealed class ExpiredRefreshTokensCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredRefreshTokensCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                ICommandHandler<DeleteExpiredRefreshTokensCommand> handler =
                    scope.ServiceProvider.GetRequiredService<ICommandHandler<DeleteExpiredRefreshTokensCommand>>();

                Result result = await handler.Handle(new DeleteExpiredRefreshTokensCommand(), stoppingToken);

                if (result.IsFailure)
                {
                    logger.LogWarning("Background job {Job} failed with {ErrorCode}", nameof(ExpiredRefreshTokensCleanupService), result.Error.Code);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Background job {Job} failed", nameof(ExpiredRefreshTokensCleanupService));
            }
        }
    }
}
```

3. **Registration** — in `app/src/Web.Api/DependencyInjection.cs` (`AddPresentation`, or a small `AddBackgroundJobs` helper):

```csharp
services.AddHostedService<ExpiredRefreshTokensCleanupService>();
```

## Rules

- A hosted service is a singleton — **never** inject `IApplicationDbContext`, handlers, or other scoped/transient services directly. Resolve them from a per-iteration scope via `IServiceScopeFactory`.
- The job resolves the handler **interface** (`ICommandHandler<...>`) so the logging/validation decorators still apply; it never queries the database itself.
- Time comes from `IDateTimeProvider` inside the handler — never `DateTime.UtcNow`.
- Wrap each iteration body in try/catch so one failure doesn't stop the loop (a background job is an allowed boundary for catching `Exception`); log with `ILogger`.
- Use `PeriodicTimer` for the schedule and always honor `stoppingToken`.
- If the use case already exists, invoke its existing command instead of creating a new one.
- Build + run the unit and architecture tests when done.
