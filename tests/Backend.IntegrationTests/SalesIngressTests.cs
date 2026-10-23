using System.Net;
using System.Net.Http.Json;
using backend.Domain.Clients;
using backend.Features.Integrations.Sales;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.IntegrationTests;

// These are source examples; this change does not start PostgreSQL or execute them.
[Collection("PostgreSQL")]
public sealed class SalesIngressTests(PostgresFixture postgres)
{
    private const string Key = "integration-only-sales-key-never-use-for-runtime-12345";

    [Fact]
    public async Task UnpaidImportReusesEmailClientAndPreservesFinancialProfile()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        using var api = Host(database);
        var request = Request(scenario) with
        {
            Client = new("12345", "student@example.test", "Must not replace staff name"), Payments = []
        };
        var response = await api.Client.PostAsJsonAsync("/integrations/sales/orders", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<SalesOrderResponse>())!.ClientCreated);
        await using var db = database.CreateContext();
        var client = await db.Clients.SingleAsync();
        Assert.Equal("Student", client.Name);
        Assert.Equal(100m, client.Financials.InitialBudget);
        Assert.Equal(200m, client.Financials.TotalEarnings);
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Empty(await db.Subscriptions.ToListAsync());
        Assert.Single(await db.ExternalAccounts.ToListAsync());
    }

    [Fact]
    public async Task OverpaymentLeavesNoNewClientOrderOrReceipt()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        using var api = Host(database);
        var request = Request(scenario);
        request = request with { Payments = [request.Payments[0] with { Amount = 200 }] };
        Assert.Equal(HttpStatusCode.Conflict, (await api.Client.PostAsJsonAsync("/integrations/sales/orders", request)).StatusCode);
        await using var db = database.CreateContext();
        Assert.Equal(1, await db.Clients.CountAsync());
        Assert.Equal(1, await db.Orders.CountAsync());
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Empty(await db.SalesOrderLinks.ToListAsync());
        Assert.Empty(await db.SalesImportReceipts.ToListAsync());
    }

    [Fact]
    public async Task ReplayReturnsTheReceiptWithoutDuplicatingBusinessRecords()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        using var api = Host(database);
        var request = Request(scenario);
        var first = await api.Client.PostAsJsonAsync("/integrations/sales/orders", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var accepted = (await first.Content.ReadFromJsonAsync<SalesOrderResponse>())!;
        var replay = await api.Client.PostAsJsonAsync("/integrations/sales/orders", request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var repeated = (await replay.Content.ReadFromJsonAsync<SalesOrderResponse>())!;
        Assert.True(repeated.Replayed);
        Assert.Equal(accepted.OrderId, repeated.OrderId);
        await using var db = database.CreateContext();
        Assert.Equal(2, await db.Clients.CountAsync()); // scenario student + imported buyer
        Assert.Single(await db.SalesOrderLinks.ToListAsync());
        Assert.Single(await db.Payments.ToListAsync());
        Assert.Equal(2, await db.Subscriptions.CountAsync());
        Assert.Single(await db.SalesImportReceipts.ToListAsync());
    }

    [Fact]
    public async Task ChangedEventPayloadConflictsAndNewEventCanAppendAPayment()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        using var api = Host(database);
        var request = Request(scenario);
        Assert.Equal(HttpStatusCode.Created, (await api.Client.PostAsJsonAsync("/integrations/sales/orders", request)).StatusCode);
        var changed = request with { Payments = [request.Payments[0] with { Amount = 30 }] };
        Assert.Equal(HttpStatusCode.Conflict, (await api.Client.PostAsJsonAsync("/integrations/sales/orders", changed)).StatusCode);
        var next = request with
        {
            EventId = "event-2", Payments = [request.Payments[0], request.Payments[0] with { ExternalId = "payment-2" }]
        };
        var response = await api.Client.PostAsJsonAsync("/integrations/sales/orders", next);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<SalesOrderResponse>())!.PaymentsCreated);
        await using var db = database.CreateContext();
        Assert.Equal(2, await db.Payments.CountAsync());
        Assert.Equal(2, await db.Subscriptions.CountAsync());
        Assert.Equal(2, await db.SalesImportReceipts.CountAsync());
    }

    [Fact]
    public async Task ConflictingPersonAndEmailDoNotMergeClientsOrCreateAnOrder()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        await using (var setup = database.CreateContext())
        {
            var other = Client.Create("Other", Email.From("other@example.test"),
                ContactInfo.Create("123456789"), new Financials(0, 0));
            setup.Clients.Add(other);
            setup.ExternalAccounts.Add(ExternalAccount.Create(scenario.Student.Id, "pipedrive", "12345"));
            await setup.SaveChangesAsync();
        }
        using var api = Host(database);
        var request = Request(scenario) with { Client = new("12345", "other@example.test", "Conflicting") };
        Assert.Equal(HttpStatusCode.Conflict, (await api.Client.PostAsJsonAsync("/integrations/sales/orders", request)).StatusCode);
        await using var db = database.CreateContext();
        Assert.Empty(await db.SalesImportReceipts.ToListAsync());
        Assert.Empty(await db.SalesOrderLinks.ToListAsync());
        Assert.Equal(1, await db.Orders.CountAsync()); // only scenario's original order
        Assert.Equal(2, await db.Clients.CountAsync());
    }

    [Fact]
    public async Task FailedDealChangeRollsBackAnEmailStagedDuringClientResolution()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        using var api = Host(database);
        var request = Request(scenario);
        Assert.Equal(HttpStatusCode.Created, (await api.Client.PostAsJsonAsync("/integrations/sales/orders", request)).StatusCode);
        var conflict = request with
        {
            EventId = "changed-deal", Client = request.Client with { Email = "new-alias@example.test" },
            Order = request.Order with { Items = [request.Order.Items[0] with { Price = 200 }] }
        };
        Assert.Equal(HttpStatusCode.Conflict, (await api.Client.PostAsJsonAsync("/integrations/sales/orders", conflict)).StatusCode);
        await using var db = database.CreateContext();
        Assert.False(await db.ClientEmails.AnyAsync(x => x.Email.Value == "new-alias@example.test"));
        Assert.Single(await db.SalesImportReceipts.ToListAsync());
    }

    private static ApiHost Host(PostgresDatabase database)
    {
        var host = new ApiHost(database, Key);
        host.Client.DefaultRequestHeaders.Add("X-Integration-Key", Key);
        return host;
    }

    private static SalesOrderRequest Request(Scenario scenario)
    {
        var date = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        return new("event-1", new("12345", "buyer@example.test", "Buyer"),
            new("67890", "USD", [new(scenario.Product.Code, scenario.Package.Code, 100, 1)], date),
            [new("payment-1", 25, "USD", "Paid", date)]);
    }
}
