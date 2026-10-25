using Application.Abstractions.Messaging;
using Application.Users.DeleteExpiredUnconfirmed;
using SharedKernel;

namespace Web.Api.BackgroundJobs;

// SPEC-0003 RN-18: only schedules; the rule lives in DeleteExpiredUnconfirmedUsersCommand.
internal sealed partial class UnconfirmedUsersCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<UnconfirmedUsersCleanupService> logger) : BackgroundService
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
                ICommandHandler<DeleteExpiredUnconfirmedUsersCommand, int> handler =
                    scope.ServiceProvider.GetRequiredService<ICommandHandler<DeleteExpiredUnconfirmedUsersCommand, int>>();

                Result<int> result = await handler.Handle(new DeleteExpiredUnconfirmedUsersCommand(), stoppingToken);

                if (result.IsFailure)
                {
                    LogFailedWithError(logger, nameof(UnconfirmedUsersCleanupService), result.Error.Code);
                }
                else
                {
                    LogRemoved(logger, result.Value);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                LogFailed(logger, exception, nameof(UnconfirmedUsersCleanupService));
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed {RemovedCount} users whose email was not confirmed in time")]
    private static partial void LogRemoved(ILogger logger, int removedCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Background job {Job} failed with {ErrorCode}")]
    private static partial void LogFailedWithError(ILogger logger, string job, string errorCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Background job {Job} failed")]
    private static partial void LogFailed(ILogger logger, Exception exception, string job);
}
