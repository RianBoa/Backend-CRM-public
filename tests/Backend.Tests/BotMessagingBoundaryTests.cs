using backend.Domain.Messaging;
using backend.Infrastructure;
using backend.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests;

public sealed class BotMessagingBoundaryTests
{
    [Theory]
    [InlineData("not-a-correlation-id")]
    [InlineData("")]
    [InlineData("f10fd48a01394454a51f3492f18d2b2f")]
    public async Task RequestCorrelationIsNormalizedAndReturned(string supplied)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = supplied;
        string? observed = null;
        var middleware = new RequestLogMiddleware(c =>
        {
            observed = RequestLogMiddleware.CorrelationId(c);
            c.Response.StatusCode = 202;
            return Task.CompletedTask;
        }, NullLogger<RequestLogMiddleware>.Instance);
        await middleware.Invoke(context);

        Assert.True(Guid.TryParseExact(observed, "N", out _));
        Assert.Equal(observed, context.Response.Headers["X-Correlation-Id"].ToString());
        if (Guid.TryParseExact(supplied, "N", out var expected)) Assert.Equal(expected.ToString("N"), observed);
        Assert.Equal(202, context.Response.StatusCode);
    }

    [Fact]
    public void PersistenceModelHasUniqueInFlightGuardAndFilteredDueIndex()
    {
        // Metadata inspection only; this test does not open a database connection.
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=unused;Password=unused").Options);
        var request = db.Model.FindEntityType(typeof(DiscordGrantRequest))!;
        var active = Assert.Single(request.GetIndexes().Where(x => x.Properties.Count == 1 &&
            x.Properties[0].Name == nameof(DiscordGrantRequest.ActiveSubscriptionId)));
        Assert.True(active.IsUnique);
        Assert.True(request.FindProperty(nameof(DiscordGrantRequest.Status))!.IsConcurrencyToken);
        var outbox = db.Model.FindEntityType(typeof(OutboxMessage))!;
        var due = Assert.Single(outbox.GetIndexes());
        Assert.Equal("ix_integration_outbox_ready", due.GetDatabaseName());
        Assert.Equal("\"PublishedAtUtc\" IS NULL AND \"DeadLetteredAtUtc\" IS NULL AND \"CanceledAtUtc\" IS NULL", due.GetFilter());
        Assert.True(outbox.FindProperty(nameof(OutboxMessage.Attempts))!.IsConcurrencyToken);
        foreach (var entity in new[] { request, outbox, db.Model.FindEntityType(typeof(InboxReceipt))! })
            Assert.DoesNotContain(entity.GetForeignKeys().SelectMany(x => x.Properties), x => x.IsShadowProperty());
    }
}
