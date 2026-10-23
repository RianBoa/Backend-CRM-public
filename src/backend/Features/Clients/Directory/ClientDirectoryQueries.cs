using backend.Domain.Clients;
using backend.Domain.Orders;
using backend.Features.Clients.Contracts;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;

namespace backend.Features.Clients.Directory;

public sealed class ClientDirectoryQueries(ApplicationDbContext dbContext)
{
    public static Dictionary<string, string[]> ValidateQuery(ClientListQuery query)
        => new backend.Features.Common.ListValidation()
            .Paging(query.Page, query.PageSize)
            .Sorting(query.SortBy, query.SortDir, AllowedSortFields)
            .Text(query.Search, "search").Text(query.Email, "email", 150)
            .Text(query.Telegram, "telegram", 100)
            .Dates(query.CreatedFrom, query.CreatedTo).Errors;

    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private static readonly string[] AllowedSortFields = ["name", "createdAtUtc"];

    public async Task<PagedResponse<ClientListItemResponse>> GetListAsync(
        ClientListQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.Page is > 0 ? query.Page.Value : DefaultPage;
        var pageSize = query.PageSize is > 0 ? Math.Min(query.PageSize.Value, MaxPageSize) : DefaultPageSize;
        var sortBy = NormalizeSortField(query.SortBy);
        var sortDir = NormalizeSortDirection(query.SortDir);

        var clientsQuery = dbContext.Clients
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            clientsQuery = clientsQuery.Where(client =>
                EF.Functions.ILike(client.Name, $"%{search}%") ||
                EF.Functions.ILike(client.Contacts.Telegram ?? string.Empty, $"%{search}%") ||
                EF.Functions.ILike(client.Contacts.PhoneNumber ?? string.Empty, $"%{search}%") ||
                EF.Functions.ILike(client.Contacts.Discord ?? string.Empty, $"%{search}%") ||
                client.Emails.Any(email => EF.Functions.ILike(email.Email.Value, $"%{search}%")));
        }

        if (!string.IsNullOrWhiteSpace(query.Telegram))
        {
            var telegram = query.Telegram.Trim();
            clientsQuery = clientsQuery.Where(client => client.Contacts.Telegram != null &&
                                                        EF.Functions.ILike(client.Contacts.Telegram, $"%{telegram}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.Email))
        {
            var email = query.Email.Trim().ToLowerInvariant();
            clientsQuery = clientsQuery.Where(client =>
                client.Emails.Any(clientEmail => EF.Functions.ILike(clientEmail.Email.Value, $"%{email}%")));
        }

        if (query.HasEmail is not null)
        {
            clientsQuery = query.HasEmail.Value
                ? clientsQuery.Where(client => client.Emails.Any())
                : clientsQuery.Where(client => !client.Emails.Any());
        }

        if (query.HasTelegram is not null)
        {
            clientsQuery = query.HasTelegram.Value
                ? clientsQuery.Where(client => client.Contacts.Telegram != null && client.Contacts.Telegram != "")
                : clientsQuery.Where(client => client.Contacts.Telegram == null || client.Contacts.Telegram == "");
        }

        if (query.CreatedFrom is not null)
        {
            var createdFromUtc = new DateTimeOffset(
                query.CreatedFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            clientsQuery = clientsQuery.Where(client => client.CreatedAtUtc >= createdFromUtc);
        }

        // Every representable client timestamp is within the inclusive maximum date.
        if (query.CreatedTo is { } createdTo && createdTo != DateOnly.MaxValue)
        {
            var createdToExclusiveUtc = new DateTimeOffset(
                createdTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            clientsQuery = clientsQuery.Where(client => client.CreatedAtUtc < createdToExclusiveUtc);
        }

        clientsQuery = ApplySorting(clientsQuery, sortBy, sortDir);

        var total = await clientsQuery.CountAsync(cancellationToken);

        var pageItems = await clientsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(client => new ClientListItemBase(
                client.Id.ToString(),
                client.ShortId,
                client.Name,
                client.Emails
                    .Where(email => email.IsPrimary)
                    .Select(email => email.Email.Value)
                    .FirstOrDefault(),
                client.Contacts.PhoneNumber,
                client.Contacts.Telegram,
                client.Contacts.Discord,
                client.CreatedAtUtc,
                client.Orders.Count(order => order.Status == OrderStatus.Active),
                client.Orders.Count(order => order.Status == OrderStatus.Graduated)))
            .ToListAsync(cancellationToken);

        var clientIds = pageItems
            .Select(item => Ulid.Parse(item.Id))
            .ToList();

        var productsByClientId = await dbContext.OrderItems
            .AsNoTracking()
            .Where(orderItem => clientIds.Contains(orderItem.Order.ClientId))
            .Select(orderItem => new
            {
                ClientId = orderItem.Order.ClientId,
                orderItem.ProductId,
                orderItem.Product.Code,
                orderItem.Product.Name
            })
            .ToListAsync(cancellationToken);

        var productLookup = productsByClientId
            .GroupBy(item => item.ClientId)
            .ToDictionary(
                group => group.Key.ToString(),
                group => group
                    .GroupBy(item => new { item.ProductId, item.Code, item.Name })
                    .Select(productGroup => new ClientProductResponse(
                        productGroup.Key.ProductId,
                        productGroup.Key.Code,
                        productGroup.Key.Name))
                    .OrderBy(product => product.Name)
                    .ToList());

        var items = pageItems
            .Select(item =>
            {
                var products = productLookup.GetValueOrDefault(item.Id) ?? [];
                return new ClientListItemResponse(
                    item.Id,
                    item.ShortId,
                    item.Name,
                    item.PrimaryEmail,
                    item.PhoneNumber,
                    item.Telegram,
                    item.Discord,
                    item.CreatedAtUtc,
                    item.ActiveOrdersCount,
                    item.CompletedOrdersCount,
                    products.Select(product => product.Name).ToList(),
                    products);
            })
            .ToList();

        return new PagedResponse<ClientListItemResponse>(items, total, page, pageSize);
    }

    public async Task<SelectResponse<ClientSelectItemResponse>> GetSelectAsync(
        string? search,
        int? limit,
        CancellationToken cancellationToken)
    {
        var take = limit is > 0 ? Math.Min(limit.Value, 100) : 50;

        var clientsQuery = dbContext.Clients
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            clientsQuery = clientsQuery.Where(client =>
                EF.Functions.ILike(client.Name, $"%{normalizedSearch}%") ||
                client.Emails.Any(email => EF.Functions.ILike(email.Email.Value, $"%{normalizedSearch}%")) ||
                EF.Functions.ILike(client.Contacts.Telegram ?? string.Empty, $"%{normalizedSearch}%"));
        }

        var items = await clientsQuery
            .OrderBy(client => client.Name)
            .Take(take)
            .Select(client => new ClientSelectItemResponse(
                client.Id.ToString(),
                client.Name,
                client.Emails
                    .Where(email => email.IsPrimary)
                    .Select(email => email.Email.Value)
                    .FirstOrDefault() ?? client.Contacts.Telegram ?? client.Contacts.PhoneNumber))
            .ToListAsync(cancellationToken);

        return new SelectResponse<ClientSelectItemResponse>(items);
    }

    private static IQueryable<Client> ApplySorting(IQueryable<Client> query, string sortBy, string sortDir)
    {
        var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy switch
        {
            "createdAtUtc" => descending
                ? query.OrderByDescending(client => client.CreatedAtUtc).ThenByDescending(client => client.Id)
                : query.OrderBy(client => client.CreatedAtUtc).ThenBy(client => client.Id),
            _ => descending
                ? query.OrderByDescending(client => client.Name).ThenByDescending(client => client.Id)
                : query.OrderBy(client => client.Name).ThenBy(client => client.Id)
        };
    }

    private static string NormalizeSortField(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return "name";
        }

        var normalized = sortBy.Trim();
        return AllowedSortFields.FirstOrDefault(field => string.Equals(field, normalized, StringComparison.OrdinalIgnoreCase))
            ?? "name";
    }

    private static string NormalizeSortDirection(string? sortDir)
        => string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

    private sealed record ClientListItemBase(
        string Id,
        string ShortId,
        string Name,
        string? PrimaryEmail,
        string? PhoneNumber,
        string? Telegram,
        string? Discord,
        DateTimeOffset CreatedAtUtc,
        int ActiveOrdersCount,
        int CompletedOrdersCount);
}
