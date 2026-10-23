using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using backend.Domain.Messaging;
using backend.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUlid;
using Xunit;

namespace Backend.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class OutboxCancellationRaceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task CancellationSharesTheDispatchRowLockAndObservesAConfirmedPublication()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        var id = await SubscriptionWorkflowRegressionTests.SeedSubscriptionAsync(database);
        var cancelLock = new CancelLockObserver();
        using var api = SubscriptionWorkflowRegressionTests.MessagingApi(database,
            s => s.AddScoped(_ => database.CreateContext(cancelLock)));
        await api.AuthenticateAsync(database, scenario.Admin);
        var requestId = Ulid.NewUlid().ToString();
        Assert.Equal(HttpStatusCode.Accepted, (await api.Client.PostAsJsonAsync($"/subscriptions/{id}/discord-access-requests",
            new { requestId, discordUserId = "123456789012345678" })).StatusCode);
        await using var dispatchContext = database.CreateContext();
        var publisher = new PausedPublisher();
        var dispatcher = new OutboxDispatcher(dispatchContext, publisher, TimeProvider.System, NullLogger<OutboxDispatcher>.Instance);
        // Fake only the network boundary; the lease and cancellation use separate real DB transactions
        var dispatch = dispatcher.DispatchOneAsync(default);
        await publisher.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var cancel = api.Client.PostAsync($"/subscriptions/{id}/cancel-pending", null);
        try
        {
            await cancelLock.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(cancel.IsCompleted);
        }
        finally { publisher.Release.TrySetResult(); }
        Assert.True(await dispatch);
        Assert.Equal(HttpStatusCode.OK, (await cancel).StatusCode);
        await using (var read = database.CreateContext())
        {
            var message = await read.IntegrationOutbox.SingleAsync();
            Assert.NotNull(message.PublishedAtUtc);
            Assert.Null(message.CanceledAtUtc);
            Assert.Equal(DiscordGrantStatus.Queued, (await read.DiscordGrantRequests.SingleAsync()).Status);
        }
        await SubscriptionWorkflowRegressionTests.ApplyLateSuccessAsync(database);
        await using var after = database.CreateContext();
        Assert.Equal(DiscordGrantStatus.NeedsReview, (await after.DiscordGrantRequests.SingleAsync()).Status);
        Assert.Empty(await after.DiscordAccesses.ToListAsync());
    }

    private sealed class PausedPublisher : IBotMessagePublisher
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task PublishAsync(OutboxMessage message, CancellationToken ct)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        }
    }

    private sealed class CancelLockObserver : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("integration_outbox", StringComparison.Ordinal) &&
                command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal)) Entered.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }
}
