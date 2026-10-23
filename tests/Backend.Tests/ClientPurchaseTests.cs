using backend.Domain.Clients;
using backend.Domain.LearningStructure;
using backend.Domain.Orders;
using backend.Domain.Products;
using backend.Domain.Subscriptions;
using backend.Features.Clients.Purchases;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using Xunit;

namespace Backend.Tests;

// These tests verify query scoping and paging, not PostgreSQL SQL translation.
public sealed class ClientPurchaseTests
{
    [Fact]
    public async Task RepeatPurchaseRemainsTwoDistinctItems()
    {
        await using var db = CreateDatabase();
        var client = AddClient(db);
        var (first, second) = AddRepeatPurchases(db, client);
        await db.SaveChangesAsync();

        var page = await new ClientPurchaseQueries(db).GetListAsync(client.Id, new(), default);

        Assert.Equal(2, page!.Total);
        Assert.Equal(2, page.Items.Select(x => x.OrderItemId).Distinct().Count());
        Assert.Contains(page.Items, x => x.OrderItemId == first.Id.ToString());
        Assert.Contains(page.Items, x => x.OrderItemId == second.Id.ToString());
    }

    [Fact]
    public async Task AnotherClientsPurchaseIsNotReturned()
    {
        await using var db = CreateDatabase();
        var owner = AddClient(db);
        var other = AddClient(db);
        var (purchase, _) = AddRepeatPurchases(db, owner);
        await db.SaveChangesAsync();
        var queries = new ClientPurchaseQueries(db);

        Assert.Null(await queries.GetDetailAsync(other.Id, purchase.Id, new(), default));
        var page = await queries.GetListAsync(other.Id, new(), default);
        Assert.Empty(page!.Items);
    }

    [Fact]
    public async Task DetailDoesNotMixSubscriptionsOrLearningFromRepeatPurchase()
    {
        await using var db = CreateDatabase();
        var client = AddClient(db);
        var (first, second) = AddRepeatPurchases(db, client);
        var firstEnrollment = Enrollment.Create(first);
        var secondEnrollment = Enrollment.Create(second);
        var firstSubscription = AddSubscription(db, client, first);
        AddSubscription(db, client, second);
        db.Enrollments.AddRange(firstEnrollment, secondEnrollment);
        await db.SaveChangesAsync();

        var detail = await new ClientPurchaseQueries(db).GetDetailAsync(client.Id, first.Id, new(), default);

        Assert.Equal(firstEnrollment.Id.ToString(), Assert.Single(detail!.Enrollments.Items).Id);
        Assert.Equal(firstSubscription.Id.ToString(), Assert.Single(detail.Subscriptions.Items).Id);
        Assert.Null(detail.Enrollments.Items[0].LearningStartAt);
        Assert.Null(detail.Enrollments.Items[0].LearningEndAt);
    }

    [Fact]
    public async Task NestedSubscriptionsHaveTheirOwnPaginationAndTotal()
    {
        await using var db = CreateDatabase();
        var client = AddClient(db);
        var (purchase, _) = AddRepeatPurchases(db, client);
        AddSubscription(db, client, purchase);
        AddSubscription(db, client, purchase);
        await db.SaveChangesAsync();

        var detail = await new ClientPurchaseQueries(db).GetDetailAsync(client.Id, purchase.Id,
            new(SubscriptionPage: 2, PageSize: 1), default);

        Assert.Equal(2, detail!.Subscriptions.Total);
        Assert.Equal(2, detail.Subscriptions.Page);
        Assert.Single(detail.Subscriptions.Items);
        Assert.Empty(detail.Enrollments.Items);
    }

    [Fact]
    public async Task MissingClientDiffersFromClientWithoutPurchases()
    {
        await using var db = CreateDatabase();
        var client = AddClient(db);
        await db.SaveChangesAsync();
        var queries = new ClientPurchaseQueries(db);

        Assert.Null(await queries.GetListAsync(Ulid.NewUlid(), new(), default));
        var page = await queries.GetListAsync(client.Id, new(), default);
        Assert.NotNull(page);
        Assert.Equal(0, page.Total);
    }

    private static ApplicationDbContext CreateDatabase() => TestDatabase.Create();

    private static Client AddClient(ApplicationDbContext db)
    {
        var client = Client.Create("Student", Email.From($"{Guid.NewGuid():N}@example.test"));
        db.Clients.Add(client);
        return client;
    }

    private static (OrderItem First, OrderItem Second) AddRepeatPurchases(ApplicationDbContext db, Client client)
    {
        var product = Product.Create("core", "Core", "Course", true) with { Id = 1 };
        var package = Package.Create("standard", "Standard", product, durationDays: 30) with { Id = 1 };
        var first = OrderItem.Create(Order.Create(client.Id, OrderSource.Manual), product, package, 100, 1);
        var second = OrderItem.Create(Order.Create(client.Id, OrderSource.Manual), product, package, 200, 1);
        db.OrderItems.AddRange(first, second);
        return (first, second);
    }

    private static Subscription AddSubscription(ApplicationDbContext db, Client client, OrderItem purchase)
    {
        var subscription = Subscription.Create(client.Id, purchase.Product, purchase,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), Subscription.SubscriptionType.CoursePlatform,
            Subscription.SubscriptionStatus.Active);
        db.Subscriptions.Add(subscription);
        return subscription;
    }
}
