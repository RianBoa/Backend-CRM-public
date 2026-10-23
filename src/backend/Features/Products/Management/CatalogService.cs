using backend.Auth;
using backend.Domain.Products;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Products.Management;

public sealed partial class CatalogService(ApplicationDbContext dbContext)
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private static readonly string[] AllowedSortFields = ["name", "code", "createdAt"];
}
