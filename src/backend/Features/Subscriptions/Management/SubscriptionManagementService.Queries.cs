using backend.Auth;
using backend.Domain.Subscriptions;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Subscriptions.Management;

public sealed partial class SubscriptionManagementService
{
    public async Task<ModuleOutcome<PagedResponse<SubscriptionListItemResponse>>> GetList(SubscriptionListQuery query,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateQuery(query);
        if (errors.Count > 0) return Invalid(errors);

        var page = query.Page is > 0 ? query.Page.Value : DefaultPage;
        var pageSize = query.PageSize is > 0 ? Math.Min(query.PageSize.Value, MaxPageSize) : DefaultPageSize;
        var sortBy = NormalizeSortField(query.SortBy);
        var sortDir = NormalizeSortDirection(query.SortDir);

        var subscriptionsQuery = dbContext.Subscriptions
            .AsNoTracking()
            .AsQueryable();

        if (TryParseUlid(query.ClientId, out var clientId))
        {
            subscriptionsQuery = subscriptionsQuery.Where(subscription => subscription.ClientId == clientId);
        }

        if (query.ProductId is not null)
        {
            subscriptionsQuery = subscriptionsQuery.Where(subscription => subscription.ProductId == query.ProductId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<Subscription.SubscriptionStatus>(query.Status.Trim(), true, out var status))
        {
            subscriptionsQuery = subscriptionsQuery.Where(subscription => subscription.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Type) &&
            Enum.TryParse<Subscription.SubscriptionType>(query.Type.Trim(), true, out var type))
        {
            subscriptionsQuery = subscriptionsQuery.Where(subscription => subscription.Type == type);
        }

        if (query.ActiveOn is not null)
        {
            var activeOn = query.ActiveOn.Value;
            subscriptionsQuery = subscriptionsQuery.Where(subscription => subscription.StartAt <= activeOn && subscription.EndAt >= activeOn);
        }

        subscriptionsQuery = ApplySorting(subscriptionsQuery, sortBy, sortDir);

        var total = await subscriptionsQuery.CountAsync(cancellationToken);

        var items = await subscriptionsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(subscription => new SubscriptionListItemResponse(
                subscription.Id.ToString(),
                subscription.ClientId.ToString(),
                subscription.Client.Name,
                subscription.ProductId,
                subscription.Product.Name,
                subscription.Type.ToString(),
                subscription.Status.ToString(),
                subscription.StartAt,
                subscription.EndAt,
                subscription.Notes,
                subscription.DiscordAccesses.Count,
                subscription.PlatformAccesses.Count))
            .ToListAsync(cancellationToken);

        return ModuleOutcome<PagedResponse<SubscriptionListItemResponse>>.Success(new PagedResponse<SubscriptionListItemResponse>(items, total, page, pageSize));
    }

    public async Task<ModuleOutcome<SubscriptionDetailResponse>> GetById(string id, CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var subscriptionId))
        {
            return Invalid(ToErrorDictionary("id", "Subscription id must be a valid ULID."));
        }

        var subscription = await BuildDetailQuery(subscriptionId).FirstOrDefaultAsync(cancellationToken);

        return subscription is null
            ? Missing(new ErrorResponse("subscription_not_found", "Subscription was not found."))
            : ModuleOutcome<SubscriptionDetailResponse>.Success(subscription);
    }
}
