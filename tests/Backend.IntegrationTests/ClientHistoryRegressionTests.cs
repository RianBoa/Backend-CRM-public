using System.Net;
using System.Text.Json;
using backend.Domain.Cases;
using backend.Domain.Clients;
using backend.Domain.Orders;
using backend.Domain.Subscriptions;
using backend.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using Xunit;

namespace Backend.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class ClientHistoryRegressionTests(PostgresFixture postgres)
{
    [Fact]
    public async Task CardIsBoundedButCountsAndAllHistoryPagesRemainComplete()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        const int count = 35;
        var date = DateTime.UtcNow.AddDays(-1);
        await using (var db = database.CreateContext())
        {
            var purchase = await db.OrderItems.Include(x => x.Order).Include(x => x.Product).Include(x => x.Package).SingleAsync();
            for (var i = 0; i < count; i++)
            {
                var item = i == 0 ? purchase : OrderItem.Create(purchase.Order, purchase.Product, purchase.Package, 100, 1);
                if (i > 0) db.OrderItems.Add(item);
                var subscription = Subscription.AwaitAccess(item, Subscription.SubscriptionType.DiscordCourseBonus, 30);
                subscription.ActivateAccess(date);
                db.Add(subscription);
                db.Add(DiscordAccess.Create(subscription.Id, "123456789012345678", "323456789012345678", "Student", date));
                db.Add(PlatformAccess.Create(subscription.Id, "Learning", "student", date));
                var result = Case.Create(scenario.Student.Id, scenario.Admin.Id, Case.CaseType.Other, 150);
                db.Add(result);
                db.Entry(result).Property(x => x.CreatedAt).CurrentValue = date;
            }
            await db.SaveChangesAsync();
        }
        var commands = new QueryCounter();
        using var api = new ApiHost(database, configure: services => services.AddScoped(_ => database.CreateContext(commands)));
        await api.AuthenticateAsync(database, scenario.Admin);
        using var card = JsonDocument.Parse(await api.Client.GetStringAsync($"/clients/{scenario.Student.Id}"));
        Assert.InRange(commands.Count, 1, 14);
        var root = card.RootElement;
        Assert.Equal(20, root.GetProperty("discordAccesses").GetArrayLength());
        Assert.Equal(20, root.GetProperty("cases").GetArrayLength());
        Assert.Equal(20, root.GetProperty("productTimeline").GetArrayLength());
        Assert.Equal(count, root.GetProperty("casesSummary").GetProperty("totalCasesCount").GetInt32());
        Assert.Equal(count, root.GetProperty("productAccessSummary").GetProperty("activeSubscriptionsCount").GetInt32());
        Assert.Equal(count, root.GetProperty("products")[0].GetProperty("subscriptionsCount").GetInt32());
        Assert.Equal(count, root.GetProperty("history").GetProperty("purchasesTotal").GetInt32());
        foreach (var (section, idField) in new[] { ("purchases", "orderItemId"), ("subscriptions", "id"),
            ("discord-accesses", "id"), ("platform-accesses", "id"), ("cases", "id") })
        {
            var url = $"/clients/{scenario.Student.Id}/{section}?pageSize=20&sortDir=desc";
            using var first = JsonDocument.Parse(await api.Client.GetStringAsync(url));
            using var second = JsonDocument.Parse(await api.Client.GetStringAsync(url + "&page=2"));
            using var repeated = JsonDocument.Parse(await api.Client.GetStringAsync(url));
            Assert.Equal(count, first.RootElement.GetProperty("total").GetInt32());
            var ids = Ids(first.RootElement, idField);
            var remaining = Ids(second.RootElement, idField);
            Assert.Equal(20, ids.Length);
            Assert.Equal(15, remaining.Length);
            Assert.Equal(ids, Ids(repeated.RootElement, idField));
            Assert.Empty(ids.Intersect(remaining));
            Assert.Equal(count, ids.Concat(remaining).Distinct().Count());
        }
    }

    [Fact]
    public async Task EmptyHistoryValidationOwnershipAndFinancialVisibilityAreEnforcedThroughHttp()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        backend.Domain.Clients.Client empty;
        User onboarding;
        await using (var db = database.CreateContext())
        {
            empty = Client.Create("Empty", Email.From("empty@example.test"), ContactInfo.Create("987654321"), new Financials(0, 0));
            onboarding = User.Create("onboarding@example.test", "@onboarding", "unused-test-hash", User.UserRole.OnBoarding, "Onboarding");
            db.AddRange(empty, onboarding, Case.Create(scenario.Student.Id, scenario.Admin.Id, Case.CaseType.Other, 999));
            await db.SaveChangesAsync();
        }
        using var api = new ApiHost(database);
        await api.AuthenticateAsync(database, onboarding);
        using var cases = JsonDocument.Parse(await api.Client.GetStringAsync($"/clients/{scenario.Student.Id}/cases"));
        Assert.Equal(JsonValueKind.Null, cases.RootElement.GetProperty("items")[0].GetProperty("amount").ValueKind);
        using var other = JsonDocument.Parse(await api.Client.GetStringAsync($"/clients/{empty.Id}/cases"));
        Assert.Equal(0, other.RootElement.GetProperty("total").GetInt32());
        Assert.Empty(other.RootElement.GetProperty("items").EnumerateArray());
        using var card = JsonDocument.Parse(await api.Client.GetStringAsync($"/clients/{empty.Id}"));
        Assert.Equal(0, card.RootElement.GetProperty("history").GetProperty("subscriptionsTotal").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync($"/clients/{NUlid.Ulid.NewUlid()}/cases")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Client.GetAsync($"/clients/{empty.Id}/subscriptions?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Client.GetAsync($"/clients/{empty.Id}/cases?productId=1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Client.GetAsync($"/clients/{empty.Id}/subscriptions?sortBy=password")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Client.GetAsync($"/clients/{empty.Id}/subscriptions?status=not-a-status")).StatusCode);
    }

    private static string[] Ids(JsonElement page, string key)
        => page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty(key).GetString()!).ToArray();

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Count);
            return ValueTask.FromResult(result);
        }
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref Count);
            return result;
        }
    }
}
