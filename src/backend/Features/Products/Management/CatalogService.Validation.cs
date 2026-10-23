using backend.Auth;
using backend.Domain.Products;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Products.Management;

public sealed partial class CatalogService
{
    private static Dictionary<string, string[]> ValidateQuery(ProductListQuery query)
        => new ListValidation()
            .Paging(query.Page, query.PageSize)
            .Sorting(query.SortBy, query.SortDir, AllowedSortFields)
            .Text(query.Search, "search")
            .Text(query.Type, "type")
            .Errors;
}
