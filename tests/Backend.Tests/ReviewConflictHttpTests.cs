using backend.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Backend.Tests;

public sealed class ReviewConflictHttpTests
{
    [Theory]
    [InlineData("40001")]
    [InlineData("40P01")]
    public async Task WrappedProviderConflictsStillMapTo409(string sqlState)
    {
        var error = new InvalidOperationException("Provider execution strategy failure",
            new DbUpdateException("Write failed", new PostgresException("Conflict", "ERROR", "ERROR", sqlState)));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(_ => Task.FromException(error), NullLogger<GlobalExceptionMiddleware>.Instance);
        await middleware.Invoke(context);
        Assert.Equal(409, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("\"code\":\"concurrent_change\"", body);
    }
}
