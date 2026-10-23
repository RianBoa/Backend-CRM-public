using System.ComponentModel.DataAnnotations;
using backend.Domain.Subscriptions;
using backend.Features.Clients.Contracts;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using ClientCase = backend.Domain.Cases.Case;

namespace backend.Features.Clients.Workspace;

public sealed record ClientHistoryQuery(
    [Range(1, 1000000)] int Page = 1,
    [Range(1, 100)] int PageSize = 20,
    [Range(1, int.MaxValue)] int? ProductId = null,
    [StringLength(32)] string? Status = null,
    [StringLength(120)] string? Search = null,
    [RegularExpression("^(date|id)$")] string SortBy = "date",
    [RegularExpression("^(asc|desc)$")] string SortDir = "desc");

public sealed record ClientPlatformAccessResponse(string Id, string SubscriptionId, int ProductId, string ProductName,
    string SubscriptionStatus, string PlatformName, string PlatformUserId, DateTime? GrantedAt);

public sealed record ClientSubscriptionHistoryResponse(string Id, string OrderItemId, int ProductId, string ProductName,
    int PackageId, string PackageName, string Type, string Status, DateTime? SubscriptionStartAt,
    DateTime? SubscriptionEndAt, int DiscordAccessRecordsCount, int PlatformAccessRecordsCount);

public sealed class ClientHistoryQueries(ApplicationDbContext db)
{
    public Task<bool> ClientExistsAsync(Ulid id, CancellationToken ct) => db.Clients.AnyAsync(x => x.Id == id, ct);

    public static Dictionary<string, string[]> Validate(ClientHistoryQuery query, bool cases)
    {
        var errors = new backend.Features.Common.ListValidation().Paging(query.Page, query.PageSize)
            .Sorting(query.SortBy, query.SortDir, ["date", "id"]).Text(query.Search, "search").Errors;
        if (query.ProductId is <= 0) errors["productId"] = ["productId must be positive."];
        if (cases && query.ProductId is not null) errors["productId"] = ["Cases are client-scoped, not product-scoped."];
        if (query.Status is not null && (cases
            ? !Enum.TryParse<ClientCase.CaseStatus>(query.Status, true, out var c) || !Enum.IsDefined(c)
            : !Enum.TryParse<Subscription.SubscriptionStatus>(query.Status, true, out var s) || !Enum.IsDefined(s)))
            errors["status"] = ["Unknown status for this history section."];
        return errors;
    }

    private IQueryable<Subscription> Subscriptions(Ulid clientId, ClientHistoryQuery request)
    {
        var query = db.Subscriptions.AsNoTracking().Where(x => x.ClientId == clientId);
        if (request.ProductId is { } productId) query = query.Where(x => x.ProductId == productId);
        if (request.Status is not null)
        {
            var status = Enum.Parse<Subscription.SubscriptionStatus>(request.Status, true);
            query = query.Where(x => x.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Product.Name, pattern));
        }
        return query;
    }

    public async Task<PagedResponse<ClientSubscriptionHistoryResponse>> GetSubscriptionsAsync(Ulid clientId, ClientHistoryQuery request, CancellationToken ct)
    {
        var query = Subscriptions(clientId, request);
        var total = await query.CountAsync(ct);
        var rows = await Page(query, request, x => x.StartAt, x => x.Id)
            .Select(x => new { x.Id, x.OrderItemId, x.ProductId, ProductName = x.Product.Name,
                x.OrderItem.PackageId, PackageName = x.OrderItem.Package.Name, x.Type, x.Status, x.StartAt, x.EndAt,
                DiscordCount = x.DiscordAccesses.Count, PlatformCount = x.PlatformAccesses.Count }).ToListAsync(ct);
        return new(rows.Select(x => new ClientSubscriptionHistoryResponse(x.Id.ToString(), x.OrderItemId.ToString(), x.ProductId,
            x.ProductName, x.PackageId, x.PackageName, x.Type.ToString(), x.Status.ToString(),
            x.StartAt, x.EndAt, x.DiscordCount, x.PlatformCount)).ToArray(), total, request.Page, request.PageSize);
    }

    public async Task<PagedResponse<ClientDiscordAccessResponse>> GetDiscordAsync(Ulid clientId, ClientHistoryQuery request, CancellationToken ct)
    {
        var subscriptions = Subscriptions(clientId, request).Select(x => x.Id);
        var query = db.DiscordAccesses.AsNoTracking().Where(x => subscriptions.Contains(x.SubscriptionId));
        var total = await query.CountAsync(ct);
        var rows = await Page(query, request, x => x.GrantedAt, x => x.Id).Select(x => new
        {
            x.Id, x.SubscriptionId, x.Subscription.ProductId, x.Subscription.Product.Name,
            x.Subscription.Status, x.DiscordUserId, x.RoleId, x.RoleName, x.GrantedAt
        }).ToListAsync(ct);
        return new(rows.Select(x => new ClientDiscordAccessResponse(x.Id.ToString(), x.SubscriptionId.ToString(),
            x.ProductId, x.Name, x.Status.ToString(), x.DiscordUserId, x.RoleId, x.RoleName, x.GrantedAt)).ToArray(), total, request.Page, request.PageSize);
    }

    public async Task<PagedResponse<ClientPlatformAccessResponse>> GetPlatformAsync(Ulid clientId, ClientHistoryQuery request, CancellationToken ct)
    {
        var subscriptions = Subscriptions(clientId, request).Select(x => x.Id);
        var query = db.PlatformAccesses.AsNoTracking().Where(x => subscriptions.Contains(x.SubscriptionId));
        var total = await query.CountAsync(ct);
        var rows = await Page(query, request, x => x.GrantedAt, x => x.Id).Select(x => new
        {
            x.Id, x.SubscriptionId, x.Subscription.ProductId, x.Subscription.Product.Name,
            x.Subscription.Status, x.PlatformName, x.PlatformUserId, x.GrantedAt
        }).ToListAsync(ct);
        return new(rows.Select(x => new ClientPlatformAccessResponse(x.Id.ToString(), x.SubscriptionId.ToString(),
            x.ProductId, x.Name, x.Status.ToString(), x.PlatformName, x.PlatformUserId, x.GrantedAt)).ToArray(), total, request.Page, request.PageSize);
    }

    public async Task<PagedResponse<ClientCaseWorkspaceItemResponse>> GetCasesAsync(Ulid clientId, ClientHistoryQuery request, bool financials, CancellationToken ct)
    {
        var query = db.Cases.AsNoTracking().Where(x => x.ClientId == clientId);
        if (request.Status is not null)
        {
            var status = Enum.Parse<ClientCase.CaseStatus>(request.Status, true);
            query = query.Where(x => x.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(x => x.Comment != null && EF.Functions.ILike(x.Comment, pattern));
        }
        var total = await query.CountAsync(ct);
        var rows = await Page(query, request, x => x.CreatedAt, x => x.Id).Select(x => new
        {
            x.Id, x.Type, Amount = financials ? (decimal?)x.Amount : null, x.Currency, x.Status,
            x.IsFeatured, x.HasConsent, x.CreatedById, x.CreatedBy.FullName, x.CreatedAt, x.ScreenshotUrl, x.TelegramUrl, x.Comment
        }).ToListAsync(ct);
        return new(rows.Select(x => new ClientCaseWorkspaceItemResponse(x.Id.ToString(), x.Type.ToString(), x.Amount,
            x.Currency, x.Status.ToString(), x.IsFeatured, x.HasConsent, x.CreatedById.ToString(), x.FullName,
            x.CreatedAt, x.ScreenshotUrl, x.TelegramUrl, x.Comment)).ToArray(), total, request.Page, request.PageSize);
    }

    private static IQueryable<T> Page<T, TDate>(IQueryable<T> query, ClientHistoryQuery request,
        System.Linq.Expressions.Expression<Func<T, TDate>> date, System.Linq.Expressions.Expression<Func<T, Ulid>> id)
    {
        var sorted = request.SortBy == "id"
            ? request.SortDir == "asc" ? query.OrderBy(id) : query.OrderByDescending(id)
            : request.SortDir == "asc" ? query.OrderBy(date).ThenBy(id) : query.OrderByDescending(date).ThenByDescending(id);
        return sorted.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize);
    }
}
