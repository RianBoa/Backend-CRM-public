using backend.Domain.Subscriptions;
using backend.Features.Clients.Contracts;

namespace backend.Features.Clients.Workspace;

internal static class ClientWorkspaceProjection
{
    internal static ClientDetailResponse Build(ClientDetailSnapshot client, ClientProductContext productContext, ClientActivityContext activity)
    {
        var orderedProducts = productContext.OrderedProducts;
        var subscriptionsResponse = productContext.Subscriptions;
        var discordAccessResponses = activity.DiscordAccesses;
        var platformAccessTimelineResponses = activity.PlatformAccesses;
        var cases = activity.Cases;
        var allProducts = productContext.AllProducts;

        var productTags = BuildProductTags(allProducts, subscriptionsResponse, platformAccessTimelineResponses.Count > 0)
            .ToList();

        var products = productContext.ProductSummaries
            .Select(product => new ClientProductRelationshipResponse(
                product.ProductId,
                product.ProductCode,
                product.ProductName,
                product.ProductType,
                product.PackageName,
                product.Status,
                product.ProductHasDiscordAccess,
                product.OrdersCount,
                product.SubscriptionsCount,
                product.ActiveSubscriptionsCount,
                product.HasDiscordAccess,
                product.HasPlatformAccess,
                product.PurchaseDate,
                product.AccessStartAt,
                product.AccessEndAt,
                product.CohortName,
                product.GroupName,
                product.LearningStartAt,
                product.LearningEndAt,
                product.OrderComment))
            .OrderBy(product => product.ProductName)
            .ThenBy(product => product.ProductId)
            .Take(ClientDetailQueries.PreviewLimit)
            .ToList();

        var productAccessSummary = new ClientProductAccessSummaryResponse(
            products.Count,
            orderedProducts.Select(item => item.ProductId).Distinct().Count(),
            subscriptionsResponse.Select(item => item.ProductId).Distinct().Count(),
            subscriptionsResponse.Count(item => string.Equals(item.Status, Subscription.SubscriptionStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase)),
            discordAccessResponses.Count,
            discordAccessResponses.Count(item => string.Equals(item.SubscriptionStatus, Subscription.SubscriptionStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase)),
            platformAccessTimelineResponses.Count);

        var casesSummary = new ClientCasesSummaryResponse(
            cases.Count,
            cases.Count(@case => @case.IsFeatured),
            cases.Count(@case => string.Equals(@case.Status, "Approved", StringComparison.OrdinalIgnoreCase)),
            cases.Count(@case => string.Equals(@case.Status, "Published", StringComparison.OrdinalIgnoreCase)),
            cases.FirstOrDefault()?.CreatedAt);

        var productTimeline = BuildProductTimeline(orderedProducts, subscriptionsResponse, discordAccessResponses, platformAccessTimelineResponses);

        return new ClientDetailResponse(
            client.Id,
            client.ShortId,
            client.Name,
            client.Emails,
            client.Contacts,
            null,
            client.AdditionalInfo,
            client.Meta,
            client.Stats,
            productTags,
            productAccessSummary,
            products,
            discordAccessResponses,
            casesSummary,
            cases,
            productTimeline);
    }

    private static IReadOnlyList<string> BuildProductTags(
        IReadOnlyList<ProductMarker> products,
        IReadOnlyList<SubscriptionSnapshot> subscriptions,
        bool hasPlatformAccess)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in products)
        {
            AddTag(tags, product.ProductCode);
            AddTag(tags, product.ProductType);

            if (product.ProductHasDiscordAccess)
            {
                tags.Add("discord");
            }
        }

        if (subscriptions.Any(item => item.DiscordAccessesCount > 0))
        {
            tags.Add("discord");
        }

        if (hasPlatformAccess)
        {
            tags.Add("platform");
        }

        return tags
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    private static List<ClientProductTimelineItemResponse> BuildProductTimeline(
        IReadOnlyList<OrderedProductSnapshot> orderedProducts,
        IReadOnlyList<SubscriptionSnapshot> subscriptions,
        IReadOnlyList<ClientDiscordAccessResponse> discordAccesses,
        IReadOnlyList<PlatformAccessTimelineSnapshot> platformAccesses)
    {
        var timeline = new List<ClientProductTimelineItemResponse>();

        timeline.AddRange(orderedProducts.Select(item => new ClientProductTimelineItemResponse(
            "order_created",
            item.OrderCreatedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            $"Ordered {item.ProductName}",
            $"Order status: {item.OrderStatus}",
            item.OrderId,
            item.ProductId,
            item.ProductName,
            item.OrderStatus)));

        timeline.AddRange(subscriptions.Where(item => item.StartAt is not null).Select(item => new ClientProductTimelineItemResponse(
            "subscription_started",
            item.StartAt!.Value,
            $"Subscription started for {item.ProductName}",
            $"Status: {item.Status}",
            item.SubscriptionId,
            item.ProductId,
            item.ProductName,
            item.Status)));

        timeline.AddRange(discordAccesses
            .Where(item => item.GrantedAt is not null)
            .Select(item => new ClientProductTimelineItemResponse(
                "discord_access_granted",
                item.GrantedAt!.Value,
                $"Discord access granted for {item.ProductName}",
                $"Role: {item.RoleName}",
                item.Id,
                item.ProductId,
                item.ProductName,
                item.SubscriptionStatus)));

        timeline.AddRange(platformAccesses
            .Where(item => item.GrantedAt is not null)
            .Select(item => new ClientProductTimelineItemResponse(
                "platform_access_granted",
                item.GrantedAt!.Value,
                $"Platform access granted for {item.ProductName}",
                $"{item.PlatformName}: {item.PlatformUserId}",
                item.Id,
                item.ProductId,
                item.ProductName,
                item.SubscriptionStatus)));

        return timeline
            .OrderByDescending(item => item.OccurredAt)
            .ThenBy(item => item.RelatedId)
            .ThenBy(item => item.Type)
            .Take(ClientDetailQueries.PreviewLimit)
            .ToList();
    }
    private static void AddTag(ISet<string> tags, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return;
        }

        var normalized = rawValue.Trim().ToLowerInvariant().Replace(' ', '-');
        tags.Add(normalized);
    }
}
