using System.Text.Json;
using backend.Domain.Messaging;
using backend.Domain.Orders;
using backend.Domain.Products;
using backend.Domain.Subscriptions;
using backend.Features.Integrations.Bots;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUlid;
using Xunit;

namespace Backend.Tests;

// Source-review scenarios. EF InMemory does not verify RabbitMQ or PostgreSQL locks.
public sealed class BotMessagingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    private const string UserId = "123456789012345678";
    private const string GuildId = "223456789012345678";
    private const string RoleId = "323456789012345678";
    private const string Correlation = "f10fd48a01394454a51f3492f18d2b2f";

    [Fact]
    public void PublishFailuresBackOffAndRequireExplicitRetryAfterFiveAttempts()
    {
        var message = OutboxMessage.Create(Ulid.NewUlid(), Correlation, BotMessages.GrantRequested, "{}", Now);
        message.Failed(Now);
        Assert.Equal(Now.AddSeconds(10), message.NextAttemptAtUtc);
        for (var i = 1; i < 5; i++) message.Failed(Now);
        Assert.NotNull(message.DeadLetteredAtUtc);
        Assert.True(message.Retry(Now.AddMinutes(1)));
        Assert.Equal(0, message.Attempts);
        Assert.Null(message.DeadLetteredAtUtc);
        message.Published(Now.AddMinutes(1));
        Assert.False(message.Retry(Now.AddMinutes(2)));
    }

    [Fact]
    public async Task RequestIsAtomicAndIdempotentWithoutStartingSubscription()
    {
        await using var db = TestDatabase.Create();
        var subscription = await SeedAsync(db);
        var service = Service(db);
        var body = new RequestDiscordGrant(Ulid.NewUlid().ToString(), UserId);
        var first = await service.RequestAsync(subscription.Id, body, Ulid.NewUlid(), Correlation, default);
        var again = await service.RequestAsync(subscription.Id, body, Ulid.NewUlid(), Correlation, default);

        Assert.Null(first.Error);
        Assert.Equal(first.Value, again.Value);
        Assert.Single(await db.DiscordGrantRequests.ToListAsync());
        Assert.Single(await db.IntegrationOutbox.ToListAsync());
        Assert.Single(await db.AuditLogs.ToListAsync());
        Assert.Null(subscription.StartAt);
        Assert.Empty(await db.DiscordAccesses.ToListAsync());
        var conflicting = await service.RequestAsync(subscription.Id,
            new(Ulid.NewUlid().ToString(), UserId), Ulid.NewUlid(), Correlation, default);
        Assert.Equal("discord_grant_pending", conflicting.Error?.Code);
    }

    [Fact]
    public async Task DisabledTransportDoesNotCreatePretendSuccess()
    {
        await using var db = TestDatabase.Create();
        var service = new DiscordGrantService(db, Options.Create(new BotMessagingOptions()),
            new Clock(), NullLogger<DiscordGrantService>.Instance);
        var result = await service.RequestAsync(Ulid.NewUlid(), new(Ulid.NewUlid().ToString(), UserId),
            Ulid.NewUlid(), Correlation, default);
        Assert.Equal("bot_messaging_disabled", result.Error?.Code);
        Assert.Empty(await db.IntegrationOutbox.ToListAsync());
    }

    [Theory]
    [InlineData("granted")]
    [InlineData("already_granted")]
    public async Task ConfirmedGrantStartsTermOnceAndDeduplicatesResult(string outcome)
    {
        await using var db = TestDatabase.Create();
        var subscription = await SeedAsync(db);
        var requestId = Ulid.NewUlid().ToString();
        await Service(db).RequestAsync(subscription.Id, new(requestId, UserId), Ulid.NewUlid(), Correlation, default);
        var handler = Handler(db);
        var envelope = Result(requestId, outcome);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, BotMessages.Json);

        Assert.Equal(ResultDisposition.Applied, await handler.HandleAsync(bytes, envelope.MessageId, default));
        Assert.Equal(ResultDisposition.Duplicate, await handler.HandleAsync(bytes, envelope.MessageId, default));
        Assert.Equal(Now.UtcDateTime, subscription.StartAt);
        Assert.Equal(Now.UtcDateTime.AddDays(30), subscription.EndAt);
        Assert.Single(await db.DiscordAccesses.ToListAsync());
        Assert.Single(await db.IntegrationInbox.ToListAsync());
        Assert.Single(await db.AuditLogs.Where(x => x.Action == "discord_grant_result").ToListAsync());
    }

    [Fact]
    public async Task WrongRoleCannotActivateSubscription()
    {
        await using var db = TestDatabase.Create();
        var subscription = await SeedAsync(db);
        var requestId = Ulid.NewUlid().ToString();
        await Service(db).RequestAsync(subscription.Id, new(requestId, UserId), Ulid.NewUlid(), Correlation, default);
        var envelope = Result(requestId, "granted");
        envelope = envelope with { Payload = envelope.Payload with { RoleId = "423456789012345678" } };
        Assert.Equal(ResultDisposition.Rejected, await Handler(db).HandleAsync(
            JsonSerializer.SerializeToUtf8Bytes(envelope, BotMessages.Json), envelope.MessageId, default));
        Assert.Null(subscription.StartAt);
        Assert.Empty(await db.IntegrationInbox.ToListAsync());
    }

    [Fact]
    public async Task CanceledSubscriptionRequiresReconciliationWhenWorkerReportsSuccess()
    {
        await using var db = TestDatabase.Create();
        var subscription = await SeedAsync(db);
        var requestId = Ulid.NewUlid().ToString();
        await Service(db).RequestAsync(subscription.Id, new(requestId, UserId), Ulid.NewUlid(), Correlation, default);
        subscription.Cancel();
        await db.SaveChangesAsync();
        var envelope = Result(requestId, "granted");
        Assert.Equal(ResultDisposition.Applied, await Handler(db).HandleAsync(
            JsonSerializer.SerializeToUtf8Bytes(envelope, BotMessages.Json), envelope.MessageId, default));
        var request = await db.DiscordGrantRequests.SingleAsync();
        Assert.Equal(DiscordGrantStatus.NeedsReview, request.Status);
        Assert.Equal("subscription_state_changed", request.ErrorCode);
        Assert.Null(subscription.StartAt);
        Assert.Empty(await db.DiscordAccesses.ToListAsync());
    }

    [Fact]
    public async Task WorkerFailureLeavesAccessWaitingAndAllowsANewRequest()
    {
        await using var db = TestDatabase.Create();
        var subscription = await SeedAsync(db);
        var requestId = Ulid.NewUlid().ToString();
        await Service(db).RequestAsync(subscription.Id, new(requestId, UserId), Ulid.NewUlid(), Correlation, default);
        var envelope = Result(requestId, "failed");
        await Handler(db).HandleAsync(JsonSerializer.SerializeToUtf8Bytes(envelope, BotMessages.Json), envelope.MessageId, default);
        Assert.Equal(Subscription.SubscriptionStatus.AwaitingAccess, subscription.Status);
        Assert.Null(subscription.StartAt);
        Assert.Null((await db.DiscordGrantRequests.SingleAsync()).ActiveSubscriptionId);
        var next = await Service(db).RequestAsync(subscription.Id, new(Ulid.NewUlid().ToString(), UserId),
            Ulid.NewUlid(), Correlation, default);
        Assert.Null(next.Error);
    }

    private static MessageEnvelope<DiscordRoleResult> Result(string requestId, string outcome) => new(
        Ulid.NewUlid().ToString(), Correlation, BotMessages.GrantResult, 1, Now,
        new(requestId, UserId, GuildId, RoleId, outcome, outcome == "failed" ? null : Now,
            outcome == "failed" ? "member_not_found" : null));

    private static DiscordGrantService Service(ApplicationDbContext db) => new(db, Options.Create(new BotMessagingOptions
    {
        Enabled = true,
        DiscordPackages = new() { ["mentor"] = new() { GuildId = GuildId, RoleId = RoleId, RoleName = "Student" } }
    }), new Clock(), NullLogger<DiscordGrantService>.Instance);

    private static DiscordResultHandler Handler(ApplicationDbContext db)
        => new(db, new Clock(), NullLogger<DiscordResultHandler>.Instance);

    private static async Task<Subscription> SeedAsync(ApplicationDbContext db)
    {
        var product = Product.Create("core", "Core", "Course", true) with { Id = 1 };
        var package = Package.Create("mentor", "Mentorship", product, durationDays: 30,
            discordAccess: true, discordDays: 30) with { Id = 1 };
        var order = Order.Create(Ulid.NewUlid(), OrderSource.Manual);
        var item = OrderItem.Create(order, product, package, 100, 1);
        var subscription = Subscription.AwaitAccess(item, Subscription.SubscriptionType.DiscordCourseBonus, 30);
        db.Subscriptions.Add(subscription);
        db.Payments.Add(Payment.Create(order.Id, 100, "USD", "Paid"));
        await db.SaveChangesAsync();
        return subscription;
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
