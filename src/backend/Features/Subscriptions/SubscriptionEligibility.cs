using backend.Domain.Orders;
using backend.Domain.Subscriptions;

namespace backend.Features.Subscriptions;

public static class SubscriptionEligibility
{
    public static IEnumerable<Subscription> ForPurchase(OrderItem purchase)
    {
        // Duration fields are configuration, not evidence of an already granted access.
        if (string.Equals(purchase.Product.Type, "Course", StringComparison.OrdinalIgnoreCase) &&
            purchase.Package.DurationDays is > 0)
            yield return Subscription.AwaitAccess(purchase, Subscription.SubscriptionType.CoursePlatform,
                purchase.Package.DurationDays.Value);

        if ((purchase.Package.DiscordAccess || purchase.Product.DiscordAccess) && purchase.Package.DiscordDays is > 0)
            yield return Subscription.AwaitAccess(purchase,
                string.Equals(purchase.Product.Type, "Course", StringComparison.OrdinalIgnoreCase)
                    ? Subscription.SubscriptionType.DiscordCourseBonus : Subscription.SubscriptionType.DiscordStandalone,
                purchase.Package.DiscordDays.Value);
    }
}
