using System.Text.Json;
using backend.Domain.Subscriptions;

namespace backend.Features.Clients.Workspace;

internal static class ClientProductProjection
{
    internal static ClientProductAggregate Build(
        ProductMarker product,
        IReadOnlyList<OrderedProductSnapshot> orderedProducts,
        IReadOnlyList<SubscriptionSnapshot> subscriptions,
        IReadOnlyList<LearningProductSnapshotDb> learningRows)
    {
        var orderRows = orderedProducts.Where(item => item.ProductId == product.ProductId).ToList();
        var subscriptionRows = subscriptions.Where(item => item.ProductId == product.ProductId).ToList();
        var learningProductRows = learningRows.Where(item => item.ProductId == product.ProductId).ToList();

        var latestOrderRow = orderRows
            .OrderByDescending(item => item.OrderCreatedAt)
            .ThenByDescending(item => item.OrderId)
            .FirstOrDefault();
        var latestSubscriptionRow = subscriptionRows
            .OrderByDescending(item => item.StartAt)
            .ThenByDescending(item => item.SubscriptionId)
            .FirstOrDefault();
        var preferredLearningRow = learningProductRows
            .OrderByDescending(item => GetLearningStatusRank(item.Status))
            .ThenByDescending(item => item.LearningStartAt ?? item.EnrolledAt ?? DateTime.MinValue)
            .ThenByDescending(item => item.EnrolledAt ?? DateTime.MinValue)
            .FirstOrDefault();

        var packageName = latestSubscriptionRow?.PackageName ?? latestOrderRow?.PackageName ?? preferredLearningRow?.PackageName;
        var status = ResolveClientProductStatus(preferredLearningRow?.Status, latestSubscriptionRow?.Status, latestOrderRow?.OrderStatus);
        var orderComment = latestOrderRow is null ? null : ExtractOrderComment(latestOrderRow.Meta);

        return new ClientProductAggregate(
            product.ProductId,
            product.ProductCode,
            product.ProductName,
            product.ProductType,
            packageName,
            status,
            product.ProductHasDiscordAccess,
            orderRows.Count,
            subscriptionRows.Count,
            subscriptionRows.Count(item => string.Equals(item.Status, Subscription.SubscriptionStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase)),
            subscriptionRows.Any(item => item.DiscordAccessesCount > 0),
            subscriptionRows.Any(item => item.PlatformAccessesCount > 0),
            orderRows.Count > 0 ? orderRows.Max(item => item.OrderCreatedAt) : null,
            subscriptionRows.Count > 0 ? subscriptionRows.Max(item => item.StartAt) : null,
            subscriptionRows.Count > 0 ? subscriptionRows.Max(item => item.EndAt) : null,
            preferredLearningRow?.CohortName,
            preferredLearningRow?.GroupName,
            preferredLearningRow?.LearningStartAt,
            preferredLearningRow?.LearningEndAt,
            orderComment);
    }

    private static string ResolveClientProductStatus(
        backend.Domain.LearningStructure.Enrollment.EnrollmentStatus? learningStatus,
        string? subscriptionStatus,
        string? orderStatus)
    {
        if (learningStatus is not null)
        {
            return learningStatus.Value.ToString().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(subscriptionStatus))
        {
            return subscriptionStatus.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(orderStatus))
        {
            return orderStatus.Trim().ToLowerInvariant();
        }

        return "unknown";
    }

    private static int GetLearningStatusRank(backend.Domain.LearningStructure.Enrollment.EnrollmentStatus status)
        => status switch
        {
            backend.Domain.LearningStructure.Enrollment.EnrollmentStatus.Active => 5,
            backend.Domain.LearningStructure.Enrollment.EnrollmentStatus.Pending => 4,
            backend.Domain.LearningStructure.Enrollment.EnrollmentStatus.Frozen => 3,
            backend.Domain.LearningStructure.Enrollment.EnrollmentStatus.Graduated => 2,
            backend.Domain.LearningStructure.Enrollment.EnrollmentStatus.Dropped => 1,
            _ => 0
        };

    private static string? ExtractOrderComment(JsonDocument? meta)
    {
        if (meta is null)
        {
            return null;
        }

        var root = meta.RootElement;
        foreach (var propertyName in new[] { "comment", "Comment", "notes", "Notes", "orderComment", "order_comment" })
        {
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }

        return null;
    }
}
