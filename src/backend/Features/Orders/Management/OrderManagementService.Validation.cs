using backend.Auth;
using backend.Domain.Auditing;
using System.Text.Json;
using backend.Domain.Orders;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Orders.Management;

public sealed partial class OrderManagementService
{
    private static Dictionary<string, string[]> ValidateQuery(OrderListQuery query)
        => new ListValidation()
            .Paging(query.Page, query.PageSize)
            .Sorting(query.SortBy, query.SortDir, AllowedSortFields)
            .Identifier(query.ClientId, "clientId")
            .OneOf(query.Status, "status", ["New", "PreLearning", "Active", "Graduated", "Frozen", "Closed"])
            .EnumValue<OrderSource>(query.Source, "source")
            .Positive(query.ProductId, "productId")
            .Dates(query.CreatedFrom, query.CreatedTo)
            .Errors;
}
