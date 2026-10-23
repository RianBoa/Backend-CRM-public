using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Domain.Orders;
using backend.Domain.Subscriptions;
using backend.Features.Integrations.Bots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUlid;
using Xunit;

namespace Backend.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class SubscriptionWorkflowRegressionTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpCancellationStopsUnpublishedDeliveryAndPreservesLateSuccessForReview(bool published)
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        var id = await SeedSubscriptionAsync(database);
        using var api = MessagingApi(database);
        await api.AuthenticateAsync(database, scenario.Admin);
        var requestId = Ulid.NewUlid().ToString();
        Assert.Equal(HttpStatusCode.Accepted, (await api.Client.PostAsJsonAsync($"/subscriptions/{id}/discord-access-requests",
            new { requestId, discordUserId = "123456789012345678" })).StatusCode);
        if (published)
        {
            await using var dispatch = database.CreateContext();
            (await dispatch.IntegrationOutbox.SingleAsync()).Published(DateTimeOffset.UtcNow);
            await dispatch.SaveChangesAsync();
        }
        var url = $"/subscriptions/{id}/cancel-pending";
        Assert.Equal(HttpStatusCode.OK, (await api.Client.PostAsync(url, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.PostAsync(url, null)).StatusCode);
        await using (var read = database.CreateContext())
        {
            Assert.Equal(Subscription.SubscriptionStatus.Canceled, (await read.Subscriptions.SingleAsync()).Status);
            var message = await read.IntegrationOutbox.SingleAsync();
            Assert.Equal(!published, message.CanceledAtUtc is not null);
            Assert.False(await read.IntegrationOutbox.AnyAsync(x => x.PublishedAtUtc == null && x.CanceledAtUtc == null && x.DeadLetteredAtUtc == null));
            Assert.Single(await read.AuditLogs.Where(x => x.Action == "cancel_pending_access").ToListAsync());
        }
        // The public HTTP command, not a direct domain cancellation, creates the state under review
        var statusResponse = await api.Client.GetAsync($"/subscriptions/{id}/discord-access-requests/{requestId}");
        var view = (await statusResponse.Content.ReadFromJsonAsync<DiscordGrantView>())!;
        Assert.Equal(published ? "Queued" : "Canceled", view.Status);
        await using (var poll = database.CreateContext())
        {
            var publisher = new UnexpectedPublisher();
            Assert.False(await new backend.Infrastructure.Messaging.OutboxDispatcher(poll, publisher, TimeProvider.System,
                NullLogger<backend.Infrastructure.Messaging.OutboxDispatcher>.Instance).DispatchOneAsync(default));
            Assert.False(publisher.Called);
        }
        await ApplyLateSuccessAsync(database);
        await using var after = database.CreateContext();
        Assert.Equal(backend.Domain.Messaging.DiscordGrantStatus.NeedsReview, (await after.DiscordGrantRequests.SingleAsync()).Status);
        Assert.Null((await after.Subscriptions.SingleAsync()).StartAt);
        Assert.Empty(await after.DiscordAccesses.ToListAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await api.Client.PostAsync(
            $"/subscriptions/{id}/discord-access-requests/{requestId}/retry-delivery", null)).StatusCode);
    }

    [Fact]
    public async Task LegacyStatusCannotReopenCanceledAccessViaSuspension()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        var id = await SeedSubscriptionAsync(database);
        DateTime? end;
        await using (var db = database.CreateContext())
        {
            var subscription = await db.Subscriptions.SingleAsync();
            subscription.ActivateAccess(DateTime.UtcNow.AddDays(-1));
            end = subscription.EndAt;
            await db.SaveChangesAsync();
        }
        using var api = new ApiHost(database);
        await api.AuthenticateAsync(database, scenario.Admin);
        var status = $"/subscriptions/{id}/status";
        Assert.Equal(HttpStatusCode.OK, (await api.Client.PostAsJsonAsync(status, new { status = "Canceled" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.PostAsJsonAsync(status, new { status = "Canceled" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.Client.PostAsJsonAsync(status, new { status = "Suspended" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.Client.PostAsync($"/subscriptions/{id}/resume-access", null)).StatusCode);
        await using var read = database.CreateContext();
        Assert.Equal(Subscription.SubscriptionStatus.Canceled, (await read.Subscriptions.SingleAsync()).Status);
        Assert.Equal(end, (await read.Subscriptions.SingleAsync()).EndAt);
        Assert.Single(await read.AuditLogs.Where(x => x.Action == "change_status").ToListAsync());
    }

    internal static ApiHost MessagingApi(PostgresDatabase database, Action<IServiceCollection>? configure = null)
        => new(database, configure: services =>
        {
            services.Configure<BotMessagingOptions>(x =>
            {
                x.Enabled = true;
                x.DiscordPackages["mentorship"] = new() { GuildId = "223456789012345678", RoleId = "323456789012345678", RoleName = "Student" };
            });
            configure?.Invoke(services);
        });

    internal static async Task<Ulid> SeedSubscriptionAsync(PostgresDatabase database)
    {
        await using var db = database.CreateContext();
        var purchase = await db.OrderItems.Include(x => x.Order).Include(x => x.Product).SingleAsync();
        var subscription = Subscription.AwaitAccess(purchase, Subscription.SubscriptionType.DiscordCourseBonus, 30);
        db.Add(subscription);
        db.Add(Payment.Create(purchase.OrderId, 100, "USD", "Paid", paidAt: DateTime.UtcNow));
        await db.SaveChangesAsync();
        return subscription.Id;
    }

    internal static async Task ApplyLateSuccessAsync(PostgresDatabase database)
    {
        await using var db = database.CreateContext();
        var request = await db.DiscordGrantRequests.SingleAsync();
        var now = DateTimeOffset.UtcNow;
        var envelope = new MessageEnvelope<DiscordRoleResult>(Ulid.NewUlid().ToString(), request.CorrelationId,
            BotMessages.GrantResult, 1, now, new(request.Id.ToString(), request.DiscordUserId, request.GuildId,
                request.RoleId, "granted", now, null));
        Assert.Equal(ResultDisposition.Applied, await new DiscordResultHandler(db, TimeProvider.System,
            NullLogger<DiscordResultHandler>.Instance).HandleAsync(JsonSerializer.SerializeToUtf8Bytes(envelope, BotMessages.Json), envelope.MessageId, default));
    }

    private sealed class UnexpectedPublisher : backend.Infrastructure.Messaging.IBotMessagePublisher
    {
        public bool Called { get; private set; }
        public Task PublishAsync(backend.Domain.Messaging.OutboxMessage message, CancellationToken ct)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }
}
