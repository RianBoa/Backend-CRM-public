using backend.Auth;
using backend.Domain.Products;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Products.Management;

public sealed partial class CatalogService
{
    public async Task<ModuleOutcome<PagedResponse<ProductListItemResponse>>> GetList(ProductListQuery query,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateQuery(query);
        if (errors.Count > 0) return Invalid(errors);

        var page = query.Page is > 0 ? query.Page.Value : DefaultPage;
        var pageSize = query.PageSize is > 0 ? Math.Min(query.PageSize.Value, MaxPageSize) : DefaultPageSize;
        var sortBy = NormalizeSortField(query.SortBy);
        var sortDir = NormalizeSortDirection(query.SortDir);

        var productsQuery = dbContext.Products
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            productsQuery = productsQuery.Where(product =>
                EF.Functions.ILike(product.Name, $"%{search}%") ||
                EF.Functions.ILike(product.Code, $"%{search}%") ||
                EF.Functions.ILike(product.LegacyCode ?? string.Empty, $"%{search}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var type = query.Type.Trim();
            productsQuery = productsQuery.Where(product => product.Type == type);
        }

        if (query.IsActive is not null)
        {
            productsQuery = productsQuery.Where(product => product.IsActive == query.IsActive.Value);
        }

        if (query.IsArchived is not null)
        {
            productsQuery = productsQuery.Where(product => product.IsArchived == query.IsArchived.Value);
        }

        productsQuery = ApplySorting(productsQuery, sortBy, sortDir);

        var total = await productsQuery.CountAsync(cancellationToken);

        var items = await productsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(product => new ProductListItemResponse(
                product.Id,
                product.Code,
                product.LegacyCode,
                product.Name,
                product.Type,
                product.IsActive,
                product.IsArchived,
                product.DiscordAccess,
                product.CreatedAt,
                dbContext.Packages.Count(package => package.ProductId == product.Id)))
            .ToListAsync(cancellationToken);

        return ModuleOutcome<PagedResponse<ProductListItemResponse>>.Success(new PagedResponse<ProductListItemResponse>(items, total, page, pageSize));
    }

    public async Task<ModuleOutcome<SelectResponse<ProductSelectItemResponse>>> GetSelect(string? search,
        bool? activeOnly,
        int? limit,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var validation = new ListValidation().Text(search, "search");
        if (limit is < 1 or > 100) validation.Errors["limit"] = ["limit must be between 1 and 100."];
        if (validation.Errors.Count > 0) return Invalid(validation.Errors);

        var take = limit is > 0 ? Math.Min(limit.Value, 100) : 50;

        var query = dbContext.Products
            .AsNoTracking()
            .AsQueryable();

        if (activeOnly is not false)
        {
            query = query.Where(product => product.IsActive && !product.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(product =>
                EF.Functions.ILike(product.Name, $"%{normalized}%") ||
                EF.Functions.ILike(product.Code, $"%{normalized}%"));
        }

        var items = await query
            .OrderBy(product => product.Name).ThenBy(product => product.Id)
            .Take(take)
            .Select(product => new ProductSelectItemResponse(
                product.Id,
                product.Name,
                product.Code,
                product.Type))
            .ToListAsync(cancellationToken);

        return ModuleOutcome<SelectResponse<ProductSelectItemResponse>>.Success(new SelectResponse<ProductSelectItemResponse>(items));
    }

    public async Task<ModuleOutcome<ProductDetailResponse>> GetById(int id, CancellationToken cancellationToken, ModuleActor actor)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.Id == id)
            .Select(product => new ProductDetailResponse(
                product.Id,
                product.Code,
                product.LegacyCode,
                product.Name,
                product.Type,
                product.IsActive,
                product.IsArchived,
                product.DiscordAccess,
                product.CreatedAt,
                dbContext.Packages
                    .Where(package => package.ProductId == product.Id)
                    .OrderBy(package => package.Name)
                    .Select(package => new PackageResponse(
                        package.Id,
                        package.Code,
                        package.Name,
                        package.DurationDays,
                        package.DiscordAccess,
                        package.DiscordDays))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return product is null
            ? Missing(new ErrorResponse("product_not_found", "Product was not found."))
            : ModuleOutcome<ProductDetailResponse>.Success(product);
    }
}
