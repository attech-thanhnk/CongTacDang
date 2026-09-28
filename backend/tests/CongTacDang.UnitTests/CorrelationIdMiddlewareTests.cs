using System.Text.Json;
using CongTacDang.Api.Logging;
using CongTacDang.Api.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CongTacDang.UnitTests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task UsesIncomingRequestId_WhenValid()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "abc-123_x.y:z";
        string? seenInPipeline = null;

        var middleware = new CorrelationIdMiddleware(
            ctx => { seenInPipeline = ctx.TraceIdentifier; return Task.CompletedTask; },
            NullLogger<CorrelationIdMiddleware>.Instance);
        await middleware.InvokeAsync(context);

        Assert.Equal("abc-123_x.y:z", seenInPipeline);
        Assert.Equal("abc-123_x.y:z", context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task GeneratesNewId_WhenHeaderMissing()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var id = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.Equal(32, id.Length);
        Assert.Equal(id, context.TraceIdentifier);
    }

    [Theory]
    [InlineData("abc\r\nInjected: 1")]
    [InlineData("<script>")]
    [InlineData("a,b")]
    public void ReplacesInvalidId(string incoming)
    {
        var id = CorrelationIdMiddleware.ResolveCorrelationId(incoming);

        Assert.NotEqual(incoming, id);
        Assert.Equal(32, id.Length);
    }

    [Fact]
    public void ReplacesTooLongId()
    {
        var incoming = new string('a', CorrelationIdMiddleware.MaxLength + 1);

        Assert.NotEqual(incoming, CorrelationIdMiddleware.ResolveCorrelationId(incoming));
    }

    [Fact]
    public async Task CorrelationId_IsAttachedToEveryLogLineOfRequest()
    {
        var directory = Path.Combine(Path.GetTempPath(), "congtacdang-tests", Guid.NewGuid().ToString("N"));
        var provider = new RollingFileLoggerProvider(new RollingFileLoggerOptions { Path = directory, FilePrefix = "test" });
        var factory = LoggerFactory.Create(builder => builder.AddProvider(provider));

        try
        {
            var appLogger = factory.CreateLogger("App");
            var context = new DefaultHttpContext();
            context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "req-42";

            var middleware = new CorrelationIdMiddleware(
                _ =>
                {
                    appLogger.LogInformation("Dòng thứ nhất");
                    appLogger.LogWarning("Dòng thứ hai {Value}", 7);
                    return Task.CompletedTask;
                },
                factory.CreateLogger<CorrelationIdMiddleware>());
            await middleware.InvokeAsync(context);
            appLogger.LogInformation("Ngoài request");
        }
        finally
        {
            factory.Dispose(); // flush hàng đợi ghi file
        }

        var file = Assert.Single(Directory.GetFiles(directory, "test-*.log"));
        var lines = File.ReadAllLines(file).Select(l => JsonDocument.Parse(l).RootElement).ToList();

        Assert.Equal(3, lines.Count);
        Assert.All(lines.Take(2), line => Assert.Equal("req-42", line.GetProperty("CorrelationId").GetString()));
        Assert.False(lines[2].TryGetProperty("CorrelationId", out _));
        Assert.Equal("7", lines[1].GetProperty("State").GetProperty("Value").GetString());

        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void FileLogger_DeletesFilesOlderThanRetention()
    {
        var directory = Path.Combine(Path.GetTempPath(), "congtacdang-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var oldFile = Path.Combine(directory, $"test-{DateTime.Now.AddDays(-10):yyyyMMdd}.log");
        var recentFile = Path.Combine(directory, $"test-{DateTime.Now.AddDays(-2):yyyyMMdd}.log");
        File.WriteAllText(oldFile, "{}");
        File.WriteAllText(recentFile, "{}");

        var provider = new RollingFileLoggerProvider(new RollingFileLoggerOptions { Path = directory, FilePrefix = "test", RetainedDays = 5 });
        provider.CreateLogger("App").LogInformation("kích hoạt ghi");
        provider.Dispose();

        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(recentFile));

        Directory.Delete(directory, recursive: true);
    }
}
