using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Web.Api.Infrastructure;

namespace IntegrationTests.Api;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandle_Should_Return499AndLogInformation_WhenClientAbortedTheRequest()
    {
        // Arrange
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        (GlobalExceptionHandler handler, TestLogger logger) = CreateHandler();
        DefaultHttpContext httpContext = CreateHttpContext(aborted.Token);

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, new OperationCanceledException(), CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(499);
        logger.Levels.ShouldBe([LogLevel.Information]);
    }

    [Fact]
    public async Task TryHandle_Should_Return500AndLogError_WhenCancellationWasNotRequestedByTheClient()
    {
        // Arrange
        (GlobalExceptionHandler handler, TestLogger logger) = CreateHandler();
        DefaultHttpContext httpContext = CreateHttpContext(CancellationToken.None);

        // Act
        await handler.TryHandleAsync(httpContext, new OperationCanceledException(), CancellationToken.None);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        logger.Levels.ShouldBe([LogLevel.Error]);
    }

    [Fact]
    public async Task TryHandle_Should_ReturnExceptionStatusAndLogWarning_WhenRequestIsBad()
    {
        // Arrange
        (GlobalExceptionHandler handler, TestLogger logger) = CreateHandler();
        DefaultHttpContext httpContext = CreateHttpContext(CancellationToken.None);
        var exception = new BadHttpRequestException("Malformed body", StatusCodes.Status400BadRequest);

        // Act
        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        logger.Levels.ShouldBe([LogLevel.Warning]);
    }

    private static readonly ServiceProvider Services = new ServiceCollection()
        .AddOptions()
        .AddProblemDetails()
        .BuildServiceProvider();

    private static (GlobalExceptionHandler Handler, TestLogger Logger) CreateHandler()
    {
        var logger = new TestLogger();

        return (new GlobalExceptionHandler(Services.GetRequiredService<IProblemDetailsService>(), logger), logger);
    }

    private static DefaultHttpContext CreateHttpContext(CancellationToken requestAborted) =>
        new()
        {
            RequestAborted = requestAborted,
            RequestServices = Services,
            Response = { Body = new MemoryStream() }
        };

    private sealed class TestLogger : ILogger<GlobalExceptionHandler>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Levels.Add(logLevel);
    }
}
