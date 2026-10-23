using backend.Domain.Products;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Integrations.Sales;

internal sealed record ResolvedSalesItem(SalesOrderItem Input, Product Product, Package Package);
internal sealed record SalesCatalogResolution(IReadOnlyList<ResolvedSalesItem>? Items, SalesImportError? Error);

public sealed class SalesCatalogResolver(ApplicationDbContext db)
{
    internal async Task<SalesCatalogResolution> ResolveAsync(IReadOnlyList<SalesOrderItem> items, CancellationToken ct)
    {
        var productCodes = items.Select(x => x.ProductCode.Trim()).Distinct().ToArray();
        var packageCodes = items.Select(x => x.PackageCode.Trim()).Distinct().ToArray();
        var packages = await db.Packages.Include(x => x.Product)
            .Where(x => productCodes.Contains(x.Product.Code) && packageCodes.Contains(x.Code)).ToListAsync(ct);
        var lookup = packages.ToLookup(x => (x.Product.Code, x.Code));
        var result = new List<ResolvedSalesItem>();
        foreach (var item in items)
        {
            var matches = lookup[(item.ProductCode.Trim(), item.PackageCode.Trim())].Take(2).ToList();
            if (matches.Count != 1)
                return new(null, new("catalog_item_unresolved", "Product/package codes must resolve to exactly one existing catalog item."));
            var package = matches[0];
            if (!package.Product.IsActive || package.Product.IsArchived)
                return new(null, new("product_unavailable", "An inactive or archived product cannot be imported as a new purchase."));
            result.Add(new(item, package.Product, package));
        }
        return new(result, null);
    }
}
