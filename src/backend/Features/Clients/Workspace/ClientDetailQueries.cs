using backend.Domain.Orders;
using backend.Features.Clients.Contracts;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;

namespace backend.Features.Clients.Workspace;

public sealed class ClientDetailQueries(
    ApplicationDbContext dbContext, ClientProductQueries products, ClientActivityQueries activity)
{
    internal const int PreviewLimit = 20;
    public async Task<ClientDetailResponse?> GetAsync(Ulid clientId, bool canViewFinancials, CancellationToken cancellationToken)
    {
        var client = await dbContext.Clients
            .AsNoTracking()
            .Where(entity => entity.Id == clientId)
            .Select(entity => new ClientDetailSnapshot(
                entity.Id.ToString(),
                entity.ShortId,
                entity.Name,
                entity.Emails
                    .OrderByDescending(email => email.IsPrimary)
                    .ThenBy(email => email.CreatedAtUtc)
                    .Select(email => new ClientEmailResponse(email.Email.Value, email.IsPrimary))
                    .ToList(),
                new ClientContactsResponse(
                    entity.Contacts.PhoneNumber,
                    entity.Contacts.Telegram,
                    entity.Contacts.Discord,
                    entity.Contacts.Instagram),
                null,
                new ClientAdditionalInfoResponse(
                    entity.AdditionalInfo == null ? null : entity.AdditionalInfo.Notes,
                    entity.AdditionalInfo == null ? null : entity.AdditionalInfo.PreferredLanguage,
                    entity.AdditionalInfo == null ? null : entity.AdditionalInfo.Source,
                    entity.AdditionalInfo == null ? null : entity.AdditionalInfo.City,
                    entity.AdditionalInfo == null ? null : entity.AdditionalInfo.BirthDate,
                    entity.AdditionalInfo == null ? null : entity.AdditionalInfo.Occupation),
                new ClientMetaResponse(
                    entity.CreatedAtUtc,
                    entity.CreatedBy),
                new ClientStatsResponse(
                    entity.Orders.Count(order => order.Status == OrderStatus.Active),
                    entity.Orders.Count(order => order.Status == OrderStatus.Graduated),
                    entity.Orders.Count(),
                    entity.Subscriptions.Count(),
                    entity.Cases.Count())))
            .FirstOrDefaultAsync(cancellationToken);

        if (client is null)
        {
            return null;
        }

        var productContext = await products.GetAsync(clientId, cancellationToken);
        var activityContext = await activity.GetAsync(clientId, canViewFinancials, cancellationToken);
        var response = ClientWorkspaceProjection.Build(client, productContext, activityContext);
        var orders = dbContext.OrderItems.Where(x => x.Order.ClientId == clientId);
        var subscriptions = dbContext.Subscriptions.Where(x => x.ClientId == clientId);
        var discord = dbContext.DiscordAccesses.Where(x => x.Subscription.ClientId == clientId);
        var platform = dbContext.PlatformAccesses.Where(x => x.Subscription.ClientId == clientId);
        var cases = dbContext.Cases.Where(x => x.ClientId == clientId);
        var orderedProductIds = orders.Select(x => x.ProductId).Distinct();
        var subscribedProductIds = subscriptions.Select(x => x.ProductId).Distinct();
        var productIds = orderedProductIds.Union(subscribedProductIds);
        // Aggregate in SQL instead of loading full histories or issuing one count per preview row
        var totals = await dbContext.Clients.Where(x => x.Id == clientId).Select(_ => new
        {
            Products = productIds.Count(), OrderedProducts = orderedProductIds.Count(), SubscribedProducts = subscribedProductIds.Count(),
            ActiveSubscriptions = subscriptions.Count(x => x.Status == backend.Domain.Subscriptions.Subscription.SubscriptionStatus.Active),
            Discord = discord.Count(), ActiveDiscord = discord.Count(x => x.Subscription.Status == backend.Domain.Subscriptions.Subscription.SubscriptionStatus.Active),
            Platform = platform.Count(), Purchases = orders.Count(), Subscriptions = subscriptions.Count(), Cases = cases.Count(),
            FeaturedCases = cases.Count(x => x.IsFeatured), ApprovedCases = cases.Count(x => x.Status == backend.Domain.Cases.Case.CaseStatus.Approved),
            PublishedCases = cases.Count(x => x.Status == backend.Domain.Cases.Case.CaseStatus.Published), LatestCase = cases.Max(x => (DateTime?)x.CreatedAt)
        }).SingleAsync(cancellationToken);
        var accessSummary = new ClientProductAccessSummaryResponse(totals.Products, totals.OrderedProducts, totals.SubscribedProducts,
            totals.ActiveSubscriptions, totals.Discord, totals.ActiveDiscord, totals.Platform);
        var casesSummary = new ClientCasesSummaryResponse(totals.Cases, totals.FeaturedCases, totals.ApprovedCases, totals.PublishedCases, totals.LatestCase);
        var markers = await dbContext.Products.Where(x => productIds.Contains(x.Id)).OrderBy(x => x.Id).Take(100)
            .Select(x => new { x.Code, x.Type, x.DiscordAccess }).ToListAsync(cancellationToken);
        var tags = markers.SelectMany(x => new[] { x.Code, x.Type }.Concat(x.DiscordAccess ? new[] { "discord" } : Array.Empty<string>()))
            .Concat(accessSummary.TotalDiscordAccessesCount > 0 ? new[] { "discord" } : Array.Empty<string>())
            .Concat(accessSummary.TotalPlatformAccessesCount > 0 ? new[] { "platform" } : Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToLowerInvariant().Replace(' ', '-'))
            .Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return response with { ProductAccessSummary = accessSummary, CasesSummary = casesSummary, ProductTags = tags,
            History = new(PreviewLimit, totals.Purchases, totals.Subscriptions,
                accessSummary.TotalDiscordAccessesCount, accessSummary.TotalPlatformAccessesCount, totals.Cases,
                $"/clients/{clientId}/purchases", $"/clients/{clientId}/subscriptions", $"/clients/{clientId}/discord-accesses",
                $"/clients/{clientId}/platform-accesses", $"/clients/{clientId}/cases", accessSummary.TotalProductsCount > 100) };
    }
}
