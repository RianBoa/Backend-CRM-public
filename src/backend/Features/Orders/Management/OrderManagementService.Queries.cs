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
    public async Task<ModuleOutcome<PagedResponse<OrderListItemResponse>>> GetList(OrderListQuery query,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateQuery(query);
        if (errors.Count > 0) return Invalid(errors);

        var page = query.Page is > 0 ? query.Page.Value : DefaultPage;
        var pageSize = query.PageSize is > 0 ? Math.Min(query.PageSize.Value, MaxPageSize) : DefaultPageSize;
        var sortBy = NormalizeSortField(query.SortBy);
        var sortDir = NormalizeSortDirection(query.SortDir);

        var ordersQuery = dbContext.Orders
            .AsNoTracking()
            .AsQueryable();

        if (TryParseUlid(query.ClientId, out var clientId))
        {
            ordersQuery = ordersQuery.Where(order => order.ClientId == clientId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = OrderStatus.FromValue(new[] { "New", "PreLearning", "Active", "Graduated", "Frozen", "Closed" }
                .First(value => string.Equals(value, query.Status.Trim(), StringComparison.OrdinalIgnoreCase)));
            ordersQuery = ordersQuery.Where(order => order.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Source) && Enum.TryParse<OrderSource>(query.Source.Trim(), true, out var source))
        {
            ordersQuery = ordersQuery.Where(order => order.Source == source);
        }

        if (query.ProductId is not null)
        {
            ordersQuery = ordersQuery.Where(order => order.Items.Any(item => item.ProductId == query.ProductId.Value));
        }

        if (query.CreatedFrom is not null)
        {
            ordersQuery = ordersQuery.Where(order => order.CreatedAt >= query.CreatedFrom.Value);
        }

        if (query.CreatedTo is not null)
        {
            ordersQuery = ordersQuery.Where(order => order.CreatedAt <= query.CreatedTo.Value);
        }

        ordersQuery = ApplySorting(ordersQuery, sortBy, sortDir);

        var total = await ordersQuery.CountAsync(cancellationToken);

        var pageItems = await ordersQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new OrderListItemBase(
                order.Id.ToString(),
                order.ClientId.ToString(),
                order.Client.Name,
                order.Status,
                order.Source,
                order.CreatedAt,
                order.Items.Count,
                order.Items.Sum(item => item.Price * item.Quantity)))
            .ToListAsync(cancellationToken);

        var orderIds = pageItems.Select(item => Ulid.Parse(item.Id)).ToList();

        var productLookup = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item => orderIds.Contains(item.OrderId))
            .Select(item => new
            {
                item.OrderId,
                item.ProductId,
                item.Product.Name
            })
            .ToListAsync(cancellationToken);

        var tagsByOrder = productLookup
            .GroupBy(item => item.OrderId.ToString())
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(x => new { x.ProductId, x.Name })
                    .Select(x => x.Key.Name)
                    .OrderBy(x => x)
                    .ToList());

        var items = pageItems
            .Select(item => new OrderListItemResponse(
                item.Id,
                item.ClientId,
                item.ClientName,
                item.Status.Value,
                item.Source.ToString(),
                item.CreatedAt,
                item.ItemsCount,
                item.TotalAmount,
                tagsByOrder.GetValueOrDefault(item.Id) ?? []))
            .ToList();

        return ModuleOutcome<PagedResponse<OrderListItemResponse>>.Success(new PagedResponse<OrderListItemResponse>(items, total, page, pageSize));
    }

    public async Task<ModuleOutcome<OrderDetailResponse>> GetById(string id, CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var orderId))
        {
            return Invalid(ToErrorDictionary("id", "Order id must be a valid ULID."));
        }

        var order = await BuildDetailAsync(orderId, cancellationToken);

        return order is null
            ? Missing(new ErrorResponse("order_not_found", "Order was not found."))
            : ModuleOutcome<OrderDetailResponse>.Success(order);
    }
}
