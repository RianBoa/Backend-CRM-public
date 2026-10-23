using backend.Domain.Clients;
using backend.Domain.Orders;
using backend.Domain.Products;
using backend.Domain.Subscriptions;
using backend.Features.Subscriptions;
using Microsoft.EntityFrameworkCore;
using NUlid;
using Xunit;

namespace Backend.Tests;

public sealed class SubscriptionLifecycleTests
{
    [Fact]
    public void AwaitingAccessHasNoDates()
    {
        var subscription = Subscription.AwaitAccess(Purchase(), Subscription.SubscriptionType.CoursePlatform, 30);
        Assert.Equal(Subscription.SubscriptionStatus.AwaitingAccess, subscription.Status);
        Assert.Null(subscription.StartAt);
        Assert.Null(subscription.EndAt);
        Assert.Equal(30, subscription.DurationDays);
    }

    [Fact]
    public void ActualGrantStartsConfiguredDuration()
    {
        var subscription = Subscription.AwaitAccess(Purchase(), Subscription.SubscriptionType.CoursePlatform, 30);
        var grantedAt = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(subscription.ActivateAccess(grantedAt));

        Assert.Equal(grantedAt, subscription.StartAt);
        Assert.Equal(grantedAt.AddDays(30), subscription.EndAt);
        Assert.Equal(Subscription.SubscriptionStatus.Active, subscription.Status);
    }

    [Fact]
    public void RepeatedGrantCannotExtendAccess()
    {
        var subscription = Subscription.AwaitAccess(Purchase(), Subscription.SubscriptionType.CoursePlatform, 30);
        var grantedAt = DateTime.UtcNow;
        subscription.ActivateAccess(grantedAt);

        Assert.False(subscription.ActivateAccess(grantedAt.AddDays(5)));
        Assert.Equal(grantedAt.AddDays(30), subscription.EndAt);
    }

    [Fact]
    public void CancelledSubscriptionCannotBeActivated()
    {
        var subscription = Subscription.AwaitAccess(Purchase(), Subscription.SubscriptionType.CoursePlatform, 30);
        subscription.Cancel();
        Assert.Throws<InvalidOperationException>(() => subscription.ActivateAccess(DateTime.UtcNow));
        Assert.Null(subscription.StartAt);
    }

    [Fact]
    public void NonUtcGrantIsRejectedWithoutChangingDates()
    {
        var subscription = Subscription.AwaitAccess(Purchase(), Subscription.SubscriptionType.CoursePlatform, 30);
        Assert.Throws<ArgumentException>(() => subscription.ActivateAccess(DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)));
        Assert.Null(subscription.EndAt);
    }

    [Fact]
    public void EligibilityCreatesIndependentPlatformAndDiscordTerms()
    {
        var purchase = Purchase(discordDays: 10);
        var subscriptions = SubscriptionEligibility.ForPurchase(purchase).ToList();
        Assert.Equal(2, subscriptions.Count);
        Assert.Equal(30, subscriptions.Single(x => x.Type == Subscription.SubscriptionType.CoursePlatform).DurationDays);
        Assert.Equal(10, subscriptions.Single(x => x.Type == Subscription.SubscriptionType.DiscordCourseBonus).DurationDays);
    }

    [Fact]
    public void ProductWithoutDurationDoesNotCreateASubscription()
    {
        Assert.Empty(SubscriptionEligibility.ForPurchase(Purchase(durationDays: null)));
    }

    [Fact]
    public async Task FirstPaidPaymentProvisionsOnceAndDoesNotActivate()
    {
        await using var db = TestDatabase.Create();
        var purchase = Purchase();
        db.Clients.Add(Client.Create("Student", Email.From("student@example.test")));
        db.OrderItems.Add(purchase);
        var payment = Payment.Create(purchase.OrderId, 10, "USD");
        db.Payments.Add(payment);
        var provisioner = new OrderSubscriptionProvisioner(db);

        Assert.True(await provisioner.SaveOrderChangesAsync(purchase.OrderId, null, default));
        Assert.Empty(await db.Subscriptions.ToListAsync());

        payment.MarkAsPaid(DateTime.UtcNow);
        Assert.True(await provisioner.SaveOrderChangesAsync(purchase.OrderId, null, default));
        var first = Assert.Single(await db.Subscriptions.ToListAsync());
        Assert.Null(first.StartAt);

        db.Payments.Add(Payment.Create(purchase.OrderId, 10, "USD", "Paid"));
        Assert.True(await provisioner.SaveOrderChangesAsync(purchase.OrderId, null, default));
        Assert.Equal(first.Id, Assert.Single(await db.Subscriptions.ToListAsync()).Id);
        Assert.Single(await db.AuditLogs.Where(x => x.Action == "await_access").ToListAsync());
    }

    [Fact]
    public async Task ActivationWritesAuditOnce()
    {
        await using var db = TestDatabase.Create();
        var subscription = Subscription.AwaitAccess(Purchase(), Subscription.SubscriptionType.CoursePlatform, 30);
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync();
        var service = new AccessActivationService(db, TimeProvider.System);
        var grantedAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        Assert.Null((await service.ActivateAsync(subscription.Id, grantedAt, null, default)).ErrorCode);
        Assert.Null((await service.ActivateAsync(subscription.Id, grantedAt, null, default)).ErrorCode);
        Assert.Single(await db.AuditLogs.Where(x => x.Action == "activate_access").ToListAsync());
    }

    [Fact]
    public async Task FutureGrantDoesNotActivateOrWriteAudit()
    {
        await using var db = TestDatabase.Create();
        var subscription = Subscription.AwaitAccess(Purchase(), Subscription.SubscriptionType.CoursePlatform, 30);
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync();
        var service = new AccessActivationService(db, TimeProvider.System);
        var result = await service.ActivateAsync(subscription.Id, DateTimeOffset.UtcNow.AddDays(1), null, default);
        Assert.Equal("future_grant", result.ErrorCode);
        Assert.Equal(Subscription.SubscriptionStatus.AwaitingAccess, subscription.Status);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public void ManualSubscriptionCannotReferenceAnotherClient()
    {
        var purchase = Purchase();
        Assert.Throws<ArgumentException>(() => Subscription.Create(Ulid.NewUlid(), purchase.Product, purchase,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), Subscription.SubscriptionType.CoursePlatform,
            Subscription.SubscriptionStatus.Active));
    }

    [Fact]
    public void PaidStatusUsesCanonicalSpelling()
    {
        Assert.Equal("Paid", Payment.Create(Ulid.NewUlid(), 10, "USD", "paid").Status);
    }

    private static OrderItem Purchase(int? durationDays = 30, int? discordDays = null)
    {
        var product = Product.Create("course", "Course", "Course", discordDays is not null) with { Id = 1 };
        var package = Package.Create("standard", "Standard", product, durationDays: durationDays,
            discordAccess: discordDays is not null, discordDays: discordDays) with { Id = 1 };
        return OrderItem.Create(Order.Create(Ulid.NewUlid(), OrderSource.Manual), product, package, 100, 1);
    }

    [Fact]
    public void ResumePreservesTermAndRepeatedResumeIsNoOp()
    {
        var subscription = Subscription.AwaitAccess(Purchase(), Subscription.SubscriptionType.CoursePlatform, 30);
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        subscription.ActivateAccess(start);
        subscription.Suspend();

        Assert.True(subscription.ResumeAccess(start.AddDays(10)));
        Assert.False(subscription.ResumeAccess(start.AddDays(11)));
        Assert.Equal(start, subscription.StartAt);
        Assert.Equal(start.AddDays(30), subscription.EndAt);
        Assert.Equal(Subscription.SubscriptionStatus.Active, subscription.Status);
    }

    [Fact]
    public void ExpiredSuspensionCannotBeResumed()
    {
        var subscription = Subscription.AwaitAccess(Purchase(), Subscription.SubscriptionType.CoursePlatform, 30);
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        subscription.ActivateAccess(start);
        subscription.Suspend();
        Assert.Throws<InvalidOperationException>(() => subscription.ResumeAccess(start.AddDays(30)));
        Assert.Equal(Subscription.SubscriptionStatus.Suspended, subscription.Status);
        Assert.Equal(start.AddDays(30), subscription.EndAt);
    }

    [Fact]
    public void ResumeCannotActivateWaitingOrCanceledAccess()
    {
        var subscription = Subscription.AwaitAccess(Purchase(), Subscription.SubscriptionType.CoursePlatform, 30);
        var now = DateTime.UtcNow;
        Assert.Throws<InvalidOperationException>(() => subscription.ResumeAccess(now));
        subscription.ActivateAccess(now.AddDays(-1));
        subscription.Cancel();
        Assert.Throws<InvalidOperationException>(() => subscription.ResumeAccess(now));
    }

    [Fact]
    public async Task ResumeSavesOneAuditWithoutExtendingAccess()
    {
        await using var db = TestDatabase.Create();
        var now = new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);
        var subscription = Subscription.AwaitAccess(Purchase(), Subscription.SubscriptionType.CoursePlatform, 30);
        subscription.ActivateAccess(now.UtcDateTime.AddDays(-10));
        subscription.Suspend();
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync();
        var service = new AccessActivationService(db, new FixedClock(now));

        var first = await service.ResumeAsync(subscription.Id, null, default);
        var second = await service.ResumeAsync(subscription.Id, null, default);

        Assert.Null(first.ErrorCode);
        Assert.Null(second.ErrorCode);
        Assert.Equal(now.UtcDateTime.AddDays(20), first.Value!.EndAt);
        Assert.Equal(first.Value, second.Value);
        Assert.Single(await db.AuditLogs.Where(x => x.Action == "resume_access").ToListAsync());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
