using backend.Domain.Orders;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;

namespace backend.Features.Clients.Purchases;

public sealed class ClientPurchaseQueries(ApplicationDbContext db)
{
    // Null means a missing client; an existing client without purchases gets an empty page.
    public async Task<PagedResponse<PurchaseItem>?> GetListAsync(
        Ulid clientId, PurchaseListQuery request, CancellationToken cancellationToken)
    {
        if (!await db.Clients.AnyAsync(x => x.Id == clientId, cancellationToken)) return null;

        var query = db.OrderItems.AsNoTracking().Where(x => x.Order.ClientId == clientId);
        if (request.ProductId is not null) query = query.Where(x => x.ProductId == request.ProductId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Product.Name, pattern) ||
                EF.Functions.ILike(x.Product.Code, pattern) || EF.Functions.ILike(x.Package.Name, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var page = Sort(query, request.SortBy, request.SortDir)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize);
        var rows = await ProjectPurchases(page).ToListAsync(cancellationToken);
        return new(rows.Select(ToPurchase).ToList(), total, request.Page, request.PageSize);
    }

    // Ownership is checked before related queries; a foreign purchase is indistinguishable from a missing one.
    public async Task<PurchaseDetail?> GetDetailAsync(
        Ulid clientId, Ulid orderItemId, PurchaseDetailQuery request, CancellationToken cancellationToken)
    {
        var row = await ProjectPurchases(db.OrderItems.AsNoTracking()
            .Where(x => x.Id == orderItemId && x.Order.ClientId == clientId))
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;

        var subscriptions = db.Subscriptions.AsNoTracking().Where(x =>
            x.OrderItemId == orderItemId && x.ClientId == clientId && x.ProductId == row.ProductId);
        var subscriptionTotal = await subscriptions.CountAsync(cancellationToken);
        var subscriptionRows = await subscriptions.OrderByDescending(x => x.StartAt).ThenBy(x => x.Id)
            .Skip((request.SubscriptionPage - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new { x.Id, x.Type, x.Status, x.StartAt, x.EndAt,
                DiscordCount = x.DiscordAccesses.Count, PlatformCount = x.PlatformAccesses.Count })
            .ToListAsync(cancellationToken);

        var enrollments = db.Enrollments.AsNoTracking().Where(x =>
            x.OrderItemId == orderItemId && x.Order.ClientId == clientId);
        var enrollmentTotal = await enrollments.CountAsync(cancellationToken);
        var enrollmentRows = await enrollments.OrderByDescending(x => x.EnrolledAt).ThenBy(x => x.Id)
            .Skip((request.EnrollmentPage - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new { x.Id, x.Status, x.CohortId, x.GroupId, x.EnrolledAt,
                CohortName = x.Cohort != null ? x.Cohort.Name : null,
                GroupName = x.Group != null ? x.Group.Name : null,
                LearningStartAt = x.Cohort != null ? (DateTime?)x.Cohort.StartAt : null,
                LearningEndAt = x.Cohort != null ? x.Cohort.EndsAt : null })
            .ToListAsync(cancellationToken);

        return new(ToPurchase(row),
            new(subscriptionRows.Select(x => new PurchaseSubscription(x.Id.ToString(), x.Type.ToString(),
                x.Status.ToString(), x.StartAt, x.EndAt, x.DiscordCount, x.PlatformCount)).ToList(),
                subscriptionTotal, request.SubscriptionPage, request.PageSize),
            new(enrollmentRows.Select(x => new PurchaseEnrollment(x.Id.ToString(), x.Status.ToString(),
                x.CohortId?.ToString(), x.CohortName, x.GroupId?.ToString(), x.GroupName,
                x.EnrolledAt, x.LearningStartAt, x.LearningEndAt)).ToList(),
                enrollmentTotal, request.EnrollmentPage, request.PageSize));
    }

    private static IOrderedQueryable<OrderItem> Sort(IQueryable<OrderItem> query, string field, string direction)
        => (field, direction) switch
        {
            ("productName", "asc") => query.OrderBy(x => x.Product.Name).ThenBy(x => x.Id),
            ("productName", "desc") => query.OrderByDescending(x => x.Product.Name).ThenBy(x => x.Id),
            ("purchaseDate", "asc") => query.OrderBy(x => x.Order.CreatedAt).ThenBy(x => x.Id),
            _ => query.OrderByDescending(x => x.Order.CreatedAt).ThenByDescending(x => x.Id)
        };

    private static IQueryable<PurchaseRow> ProjectPurchases(IQueryable<OrderItem> query) => query.Select(x =>
        new PurchaseRow(x.Id, x.OrderId, x.ProductId, x.Product.Code, x.Product.Name, x.Product.Type,
            x.PackageId, x.Package.Name, x.Quantity, x.Order.CreatedAt, x.Order.Status));

    private static PurchaseItem ToPurchase(PurchaseRow row) => new(
        row.OrderItemId.ToString(), row.OrderId.ToString(), row.ProductId, row.ProductCode,
        row.ProductName, row.ProductType, row.PackageId, row.PackageName,
        row.Quantity, row.PurchaseDate, row.Status.Value);

    private sealed record PurchaseRow(Ulid OrderItemId, Ulid OrderId, int ProductId, string ProductCode,
        string ProductName, string ProductType, int PackageId, string PackageName,
        uint Quantity, DateOnly PurchaseDate, OrderStatus Status);
}
