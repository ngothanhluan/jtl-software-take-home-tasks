using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using WorkTracker.Host;
using WorkTracker.Shared;
using static WorkTracker.Api.Tests.TestSupport;

namespace WorkTracker.Api.Tests;

public class ErrorLoggingTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Expected_domain_errors_are_not_logged_as_errors()
    {
        var logs = new CapturingLoggerProvider();
        var client = factory.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs))).CreateClient();

        var response = await client.PostWithKeyAsync("/users", new { username = "a" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        logs.Entries.ShouldNotContain(e => e.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task Unexpected_errors_are_logged_with_the_exception()
    {
        var logs = new CapturingLoggerProvider();
        var handler = new ProblemDetailsExceptionHandler(new LoggerFactory([logs]).CreateLogger<ProblemDetailsExceptionHandler>());
        var context = new DefaultHttpContext { RequestServices = factory.Services };
        var exception = new InvalidOperationException("boom");

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        logs.Entries.ShouldContain(e => e.Level == LogLevel.Error && e.Exception == exception);
    }

    [Fact]
    public async Task Domain_errors_handled_directly_are_not_logged()
    {
        var logs = new CapturingLoggerProvider();
        var handler = new ProblemDetailsExceptionHandler(new LoggerFactory([logs]).CreateLogger<ProblemDetailsExceptionHandler>());
        var context = new DefaultHttpContext { RequestServices = factory.Services };

        await handler.TryHandleAsync(context, new ConflictException("taken"), CancellationToken.None);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        logs.Entries.ShouldBeEmpty();
    }
}

internal sealed record LogEntry(LogLevel Level, Exception? Exception);

// Records every log entry so tests can check what was logged and at which level.
internal sealed class CapturingLoggerProvider : ILoggerProvider, ILogger
{
    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => this;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Entries.Enqueue(new LogEntry(logLevel, exception));

    public void Dispose() { }
}
