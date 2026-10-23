using backend.Domain.Subscriptions;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;

namespace backend.Features.Clients.Workspace;

public sealed class ClientProductQueries(ApplicationDbContext dbContext)
{
    internal async Task<ClientProductContext> GetAsync(Ulid clientId, CancellationToken cancellationToken)
    {
        var orderedProducts = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item => item.Order.ClientId == clientId)
            .OrderByDescending(x => x.Order.CreatedAt).ThenByDescending(x => x.Id)
            .Take(ClientDetailQueries.PreviewLimit)
            .Select(item => new OrderedProductSnapshot(
                item.OrderId.ToString(),
                item.ProductId,
                item.Product.Code,
                item.Product.Name,
                item.Product.Type,
                item.Package.Name,
                item.Product.DiscordAccess,
                item.Order.CreatedAt,
                item.Order.Status.Value,
                item.Meta))
            .ToListAsync(cancellationToken);

        var subscriptions = await dbContext.Subscriptions
            .AsNoTracking()
            .Where(subscription => subscription.ClientId == clientId)
            .OrderByDescending(x => x.StartAt).ThenByDescending(x => x.Id)
            .Take(ClientDetailQueries.PreviewLimit)
            .Select(subscription => new SubscriptionSnapshotDb(
                subscription.Id,
                subscription.ProductId,
                subscription.Product.Code,
                subscription.Product.Name,
                subscription.Product.Type,
                subscription.OrderItem.Package.Name,
                subscription.Product.DiscordAccess,
                subscription.Type,
                subscription.Status,
                subscription.StartAt,
                subscription.EndAt,
                subscription.Notes,
                subscription.DiscordAccesses.Count,
                subscription.PlatformAccesses.Count))
            .ToListAsync(cancellationToken);

        var subscriptionsResponse = subscriptions
            .Select(subscription => new SubscriptionSnapshot(
                subscription.SubscriptionId.ToString(),
                subscription.ProductId,
                subscription.ProductCode,
                subscription.ProductName,
                subscription.ProductType,
                subscription.PackageName,
                subscription.ProductHasDiscordAccess,
                subscription.Type.ToString(),
                subscription.Status.ToString(),
                subscription.StartAt,
                subscription.EndAt,
                subscription.Notes,
                subscription.DiscordAccessesCount,
                subscription.PlatformAccessesCount))
            .ToList();

        var learningRows = await dbContext.Enrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Order.ClientId == clientId && enrollment.OrderItemId.HasValue)
            .OrderByDescending(x => x.EnrolledAt).ThenByDescending(x => x.Id)
            .Take(ClientDetailQueries.PreviewLimit)
            .Select(enrollment => new LearningProductSnapshotDb(
                enrollment.OrderItem!.ProductId,
                enrollment.OrderItem.Package.Name,
                enrollment.Status,
                enrollment.DesiredStartDate,
                enrollment.EnrolledAt,
                enrollment.Cohort != null ? enrollment.Cohort.Name : null,
                enrollment.Cohort != null ? enrollment.Cohort.StartAt : (DateTime?)null,
                enrollment.Cohort != null ? enrollment.Cohort.EndsAt : null,
                enrollment.Group != null ? enrollment.Group.Name : null,
                enrollment.OrderId))
            .ToListAsync(cancellationToken);

        var allProducts = orderedProducts
            .Select(item => new ProductMarker(item.ProductId, item.ProductCode, item.ProductName, item.ProductType, item.ProductHasDiscordAccess))
            .Concat(subscriptionsResponse.Select(item => new ProductMarker(item.ProductId, item.ProductCode, item.ProductName, item.ProductType, item.ProductHasDiscordAccess)))
            .GroupBy(item => item.ProductId)
            .Select(group => group.First())
            .ToList();

        var productSummaries = allProducts
            .Select(product => ClientProductProjection.Build(product, orderedProducts, subscriptionsResponse, learningRows))
            .ToList();

        // Preview rows are bounded, but counters and access flags still describe the full product history
        var ids = productSummaries.Select(x => x.ProductId).ToArray();
        var totals = await dbContext.Products.Where(x => ids.Contains(x.Id)).Select(p => new
        {
            p.Id,
            Orders = dbContext.OrderItems.Count(x => x.Order.ClientId == clientId && x.ProductId == p.Id),
            Subscriptions = dbContext.Subscriptions.Count(x => x.ClientId == clientId && x.ProductId == p.Id),
            Active = dbContext.Subscriptions.Count(x => x.ClientId == clientId && x.ProductId == p.Id && x.Status == Subscription.SubscriptionStatus.Active),
            Discord = dbContext.DiscordAccesses.Any(x => x.Subscription.ClientId == clientId && x.Subscription.ProductId == p.Id),
            Platform = dbContext.PlatformAccesses.Any(x => x.Subscription.ClientId == clientId && x.Subscription.ProductId == p.Id),
            PurchaseDate = dbContext.OrderItems.Where(x => x.Order.ClientId == clientId && x.ProductId == p.Id).Max(x => (DateOnly?)x.Order.CreatedAt),
            StartAt = dbContext.Subscriptions.Where(x => x.ClientId == clientId && x.ProductId == p.Id).Max(x => x.StartAt),
            EndAt = dbContext.Subscriptions.Where(x => x.ClientId == clientId && x.ProductId == p.Id).Max(x => x.EndAt)
        }).ToDictionaryAsync(x => x.Id, cancellationToken);
        productSummaries = productSummaries.Select(p => p with
        {
            OrdersCount = totals[p.ProductId].Orders, SubscriptionsCount = totals[p.ProductId].Subscriptions,
            ActiveSubscriptionsCount = totals[p.ProductId].Active, HasDiscordAccess = totals[p.ProductId].Discord,
            HasPlatformAccess = totals[p.ProductId].Platform, PurchaseDate = totals[p.ProductId].PurchaseDate,
            AccessStartAt = totals[p.ProductId].StartAt, AccessEndAt = totals[p.ProductId].EndAt
        }).ToList();

        return new ClientProductContext(orderedProducts, subscriptionsResponse, allProducts, productSummaries);
    }
}
