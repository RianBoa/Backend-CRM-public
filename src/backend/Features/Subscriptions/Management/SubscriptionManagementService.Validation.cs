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
    private static Dictionary<string, string[]> ValidateQuery(SubscriptionListQuery query)
        => new ListValidation()
            .Paging(query.Page, query.PageSize)
            .Sorting(query.SortBy, query.SortDir, AllowedSortFields)
            .Identifier(query.ClientId, "clientId")
            .Positive(query.ProductId, "productId")
            .EnumValue<Subscription.SubscriptionStatus>(query.Status, "status")
            .EnumValue<Subscription.SubscriptionType>(query.Type, "type")
            .Utc(query.ActiveOn, "activeOn")
            .Errors;
}
