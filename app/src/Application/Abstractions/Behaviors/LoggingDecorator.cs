using System.Diagnostics;
using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SharedKernel;

namespace Application.Abstractions.Behaviors;

internal static partial class LoggingDecorator
{
    private const string CommandKind = "command";
    private const string QueryKind = "query";

    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        ILogger<CommandHandler<TCommand, TResponse>> logger)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken) =>
            LogAsync(logger, CommandKind, typeof(TCommand).Name, () => innerHandler.Handle(command, cancellationToken));
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        ILogger<CommandBaseHandler<TCommand>> logger)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public Task<Result> Handle(TCommand command, CancellationToken cancellationToken) =>
            LogAsync(logger, CommandKind, typeof(TCommand).Name, () => innerHandler.Handle(command, cancellationToken));
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken) =>
            LogAsync(logger, QueryKind, typeof(TQuery).Name, () => innerHandler.Handle(query, cancellationToken));
    }

    private static async Task<TResult> LogAsync<TResult>(
        ILogger logger,
        string requestKind,
        string requestName,
        Func<Task<TResult>> handle)
        where TResult : Result
    {
        long startTimestamp = Stopwatch.GetTimestamp();

        LogProcessing(logger, requestKind, requestName);

        TResult result = await handle();

        double elapsedMilliseconds = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        if (result.IsSuccess)
        {
            LogCompleted(logger, requestKind, requestName, elapsedMilliseconds);
        }
        else
        {
            // Expected failures (4xx) are the client's problem; only unexpected failures are errors.
            LogLevel level = result.Error.Type == ErrorType.Failure ? LogLevel.Error : LogLevel.Warning;

            using (LogContext.PushProperty("Error", result.Error, true))
            {
                LogCompletedWithError(logger, level, requestKind, requestName, elapsedMilliseconds);
            }
        }

        return result;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing {RequestKind} {RequestName}")]
    private static partial void LogProcessing(ILogger logger, string requestKind, string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Completed {RequestKind} {RequestName} in {ElapsedMilliseconds} ms")]
    private static partial void LogCompleted(ILogger logger, string requestKind, string requestName, double elapsedMilliseconds);

    [LoggerMessage(Message = "Completed {RequestKind} {RequestName} with error in {ElapsedMilliseconds} ms")]
    private static partial void LogCompletedWithError(
        ILogger logger,
        LogLevel level,
        string requestKind,
        string requestName,
        double elapsedMilliseconds);
}
