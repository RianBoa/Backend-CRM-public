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
    private IQueryable<SubscriptionDetailResponse> BuildDetailQuery(Ulid subscriptionId)
        => dbContext.Subscriptions
            .AsNoTracking()
            .Where(subscription => subscription.Id == subscriptionId)
            .Select(subscription => new SubscriptionDetailResponse(
                subscription.Id.ToString(),
                subscription.ClientId.ToString(),
                subscription.Client.Name,
                subscription.ProductId,
                subscription.Product.Name,
                subscription.OrderItemId.ToString(),
                subscription.Type.ToString(),
                subscription.Status.ToString(),
                subscription.StartAt,
                subscription.EndAt,
                subscription.Notes,
                subscription.DiscordAccesses
                    .OrderByDescending(access => access.GrantedAt)
                    .Select(access => new DiscordAccessResponse(
                        access.Id.ToString(),
                        access.DiscordUserId,
                        access.RoleId,
                        access.RoleName,
                        access.GrantedAt))
                    .ToList(),
                subscription.PlatformAccesses
                    .OrderByDescending(access => access.GrantedAt)
                    .Select(access => new PlatformAccessResponse(
                        access.Id.ToString(),
                        access.PlatformName,
                        access.PlatformUserId,
                        access.GrantedAt,
                        access.Notes))
                    .ToList(),
                subscription.Adjustments
                    .OrderByDescending(adjustment => adjustment.CreatedAt)
                    .Select(adjustment => new SubscriptionAdjustmentResponse(
                        adjustment.Id.ToString(),
                        adjustment.Type.ToString(),
                        adjustment.AdjustmentDate,
                        adjustment.DaysChanged,
                        adjustment.OldEndDate,
                        adjustment.NewEndDate,
                        adjustment.NewRole,
                        adjustment.Reason,
                        adjustment.Source,
                        adjustment.RelatedOrderId.HasValue ? adjustment.RelatedOrderId.Value.ToString() : null,
                        adjustment.CreatedBy,
                        adjustment.CreatedAt))
                    .ToList()));

    private static IQueryable<Subscription> ApplySorting(IQueryable<Subscription> query, string sortBy, string sortDir)
    {
        var descending = string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        return sortBy switch
        {
            "endAt" => descending ? query.OrderByDescending(subscription => subscription.EndAt).ThenByDescending(subscription => subscription.Id) : query.OrderBy(subscription => subscription.EndAt).ThenBy(subscription => subscription.Id),
            "status" => descending ? query.OrderByDescending(subscription => subscription.Status).ThenByDescending(subscription => subscription.Id) : query.OrderBy(subscription => subscription.Status).ThenBy(subscription => subscription.Id),
            _ => descending ? query.OrderByDescending(subscription => subscription.StartAt).ThenByDescending(subscription => subscription.Id) : query.OrderBy(subscription => subscription.StartAt).ThenBy(subscription => subscription.Id)
        };
    }

    private static string NormalizeSortField(string? sortBy)
        => string.IsNullOrWhiteSpace(sortBy) ? "startAt" : (AllowedSortFields.FirstOrDefault(field => string.Equals(field, sortBy.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "startAt");

    private static string NormalizeSortDirection(string? sortDir)
        => string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

    private static bool TryParseUlid(string? value, out Ulid ulid)
        => Ulid.TryParse(value?.Trim(), out ulid);

    private static Dictionary<string, string[]> ValidateCreateSubscriptionRequest(CreateSubscriptionRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            errors["clientId"] = ["Client id is required."];
        }

        if (string.IsNullOrWhiteSpace(request.OrderItemId))
        {
            errors["orderItemId"] = ["Order item id is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            errors["type"] = ["Type is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            errors["status"] = ["Status is required."];
        }

        if (request.EndAt <= request.StartAt)
        {
            errors["endAt"] = ["EndAt must be later than StartAt."];
        }

        return errors;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
