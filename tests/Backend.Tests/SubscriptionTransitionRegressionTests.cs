using backend.Domain.Orders;
using backend.Domain.Products;
using backend.Domain.Subscriptions;
using NUlid;
using Xunit;

namespace Backend.Tests;

public sealed class SubscriptionTransitionRegressionTests
{
    [Fact]
    public void CanceledAccessCannotBeSuspendedOrResumedThroughAnyDomainCommand()
    {
        var subscription = Waiting();
        var now = DateTime.UtcNow;
        subscription.ActivateAccess(now);
        Assert.True(subscription.Cancel());
        Assert.False(subscription.Cancel());
        Assert.Throws<InvalidOperationException>(() => subscription.Suspend());
        Assert.Throws<InvalidOperationException>(() => subscription.ChangeStatus(Subscription.SubscriptionStatus.Suspended, now));
        Assert.Throws<InvalidOperationException>(() => subscription.ResumeAccess(now));
        Assert.Throws<InvalidOperationException>(() => subscription.ChangeStatus(Subscription.SubscriptionStatus.Active, now));
        Assert.Equal(now, subscription.StartAt);
        Assert.Equal(now.AddDays(30), subscription.EndAt);
    }

    [Fact]
    public void ResumeAndSameStatusCommandsAreIdempotentWithoutExtendingTheTerm()
    {
        var subscription = Waiting();
        var now = DateTime.UtcNow;
        subscription.ActivateAccess(now.AddDays(-1));
        var end = subscription.EndAt;
        Assert.True(subscription.Suspend());
        Assert.False(subscription.Suspend());
        Assert.True(subscription.ResumeAccess(now));
        Assert.False(subscription.ResumeAccess(now));
        Assert.False(subscription.ChangeStatus(Subscription.SubscriptionStatus.Active, now));
        Assert.Equal(end, subscription.EndAt);
    }

    [Fact]
    public void CancelPendingCannotBeUsedAsAnActiveCancellationOrRestart()
    {
        var waiting = Waiting();
        Assert.True(waiting.CancelPending());
        Assert.False(waiting.CancelPending());
        Assert.Null(waiting.StartAt);
        Assert.Throws<InvalidOperationException>(() => waiting.ActivateAccess(DateTime.UtcNow));
        var active = Waiting();
        active.ActivateAccess(DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => active.CancelPending());
    }

    private static Subscription Waiting()
    {
        var product = Product.Create("core", "Core", "Course", true) with { Id = 1 };
        var package = Package.Create("mentor", "Mentor", product, 30, true, 30) with { Id = 1 };
        return Subscription.AwaitAccess(OrderItem.Create(Order.Create(Ulid.NewUlid(), OrderSource.Manual),
            product, package, 100, 1), Subscription.SubscriptionType.DiscordCourseBonus, 30);
    }
}
