using Application.Abstractions.Behaviors;
using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.UnitTests.Abstractions;

public sealed class LoggingDecoratorTests
{
    public sealed record TestCommand : ICommand<Guid>;

    [Fact]
    public async Task Handle_Should_LogInformation_WhenCommandSucceeds()
    {
        // Arrange
        var logger = new TestLogger<LoggingDecorator.CommandHandler<TestCommand, Guid>>();
        LoggingDecorator.CommandHandler<TestCommand, Guid> decorator = CreateDecorator(Guid.NewGuid(), logger);

        // Act
        await decorator.Handle(new TestCommand(), CancellationToken.None);

        // Assert
        logger.Entries[^1].Level.ShouldBe(LogLevel.Information);
        logger.Entries[^1].Message.ShouldStartWith("Completed command TestCommand in ");
    }

    [Fact]
    public async Task Handle_Should_LogWarning_WhenCommandFailsWithExpectedError()
    {
        // Arrange
        var logger = new TestLogger<LoggingDecorator.CommandHandler<TestCommand, Guid>>();
        LoggingDecorator.CommandHandler<TestCommand, Guid> decorator =
            CreateDecorator(Result.Failure<Guid>(Error.NotFound("Tests.NotFound", "Not found")), logger);

        // Act
        await decorator.Handle(new TestCommand(), CancellationToken.None);

        // Assert
        logger.Entries[^1].Level.ShouldBe(LogLevel.Warning);
        logger.Entries[^1].Message.ShouldStartWith("Completed command TestCommand with error in ");
    }

    [Fact]
    public async Task Handle_Should_LogError_WhenCommandFailsWithUnexpectedError()
    {
        // Arrange
        var logger = new TestLogger<LoggingDecorator.CommandHandler<TestCommand, Guid>>();
        LoggingDecorator.CommandHandler<TestCommand, Guid> decorator =
            CreateDecorator(Result.Failure<Guid>(Error.Failure("Tests.Failure", "Boom")), logger);

        // Act
        await decorator.Handle(new TestCommand(), CancellationToken.None);

        // Assert
        logger.Entries[^1].Level.ShouldBe(LogLevel.Error);
    }

    private static LoggingDecorator.CommandHandler<TestCommand, Guid> CreateDecorator(
        Result<Guid> result,
        ILogger<LoggingDecorator.CommandHandler<TestCommand, Guid>> logger)
    {
        ICommandHandler<TestCommand, Guid> innerHandler = Substitute.For<ICommandHandler<TestCommand, Guid>>();
        innerHandler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()).Returns(result);

        return new LoggingDecorator.CommandHandler<TestCommand, Guid>(innerHandler, logger);
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
