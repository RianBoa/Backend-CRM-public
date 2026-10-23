using backend.Features.Clients.Contracts;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;

namespace backend.Features.Clients.Workspace;

public sealed class ClientActivityQueries(ApplicationDbContext dbContext)
{
    internal async Task<ClientActivityContext> GetAsync(Ulid clientId, bool canViewFinancials, CancellationToken cancellationToken)
    {
        var discordAccesses = await dbContext.DiscordAccesses
            .AsNoTracking()
            .Where(access => access.Subscription.ClientId == clientId)
            .OrderByDescending(access => access.GrantedAt)
            .ThenByDescending(access => access.Id)
            .Take(ClientDetailQueries.PreviewLimit)
            .Select(access => new DiscordAccessSnapshotDb(
                access.Id,
                access.SubscriptionId,
                access.Subscription.ProductId,
                access.Subscription.Product.Name,
                access.Subscription.Status,
                access.DiscordUserId,
                access.RoleId,
                access.RoleName,
                access.GrantedAt))
            .ToListAsync(cancellationToken);

        var discordAccessResponses = discordAccesses
            .Select(access => new ClientDiscordAccessResponse(
                access.Id.ToString(),
                access.SubscriptionId.ToString(),
                access.ProductId,
                access.ProductName,
                access.SubscriptionStatus.ToString(),
                access.DiscordUserId,
                access.RoleId,
                access.RoleName,
                access.GrantedAt))
            .ToList();

        var platformAccessTimelineRows = await dbContext.PlatformAccesses
            .AsNoTracking()
            .Where(access => access.Subscription.ClientId == clientId)
            .OrderByDescending(access => access.GrantedAt).ThenByDescending(access => access.Id)
            .Take(ClientDetailQueries.PreviewLimit)
            .Select(access => new PlatformAccessTimelineSnapshotDb(
                access.Id,
                access.SubscriptionId,
                access.Subscription.ProductId,
                access.Subscription.Product.Name,
                access.Subscription.Status,
                access.PlatformName,
                access.PlatformUserId,
                access.GrantedAt))
            .ToListAsync(cancellationToken);

        var platformAccessTimelineResponses = platformAccessTimelineRows
            .Select(access => new PlatformAccessTimelineSnapshot(
                access.Id.ToString(),
                access.SubscriptionId.ToString(),
                access.ProductId,
                access.ProductName,
                access.SubscriptionStatus.ToString(),
                access.PlatformName,
                access.PlatformUserId,
                access.GrantedAt))
            .ToList();

        var caseRows = await dbContext.Cases
            .AsNoTracking()
            .Where(@case => @case.ClientId == clientId)
            .OrderByDescending(@case => @case.CreatedAt)
            .ThenByDescending(@case => @case.Id)
            .Take(ClientDetailQueries.PreviewLimit)
            .Select(@case => new ClientCaseWorkspaceItemSnapshotDb(
                @case.Id,
                @case.Type,
                canViewFinancials ? @case.Amount : (decimal?)null,
                @case.Currency,
                @case.Status,
                @case.IsFeatured,
                @case.HasConsent,
                @case.CreatedById,
                @case.CreatedBy.FullName,
                @case.CreatedAt,
                @case.ScreenshotUrl,
                @case.TelegramUrl,
                @case.Comment))
            .ToListAsync(cancellationToken);

        var cases = caseRows
            .Select(@case => new ClientCaseWorkspaceItemResponse(
                @case.Id.ToString(),
                @case.Type.ToString(),
                @case.Amount,
                @case.Currency,
                @case.Status.ToString(),
                @case.IsFeatured,
                @case.HasConsent,
                @case.CreatedById.ToString(),
                @case.CreatedByName,
                @case.CreatedAt,
                @case.ScreenshotUrl,
                @case.TelegramUrl,
                @case.Comment))
            .ToList();

        return new(discordAccessResponses, platformAccessTimelineResponses, cases);
    }
}
