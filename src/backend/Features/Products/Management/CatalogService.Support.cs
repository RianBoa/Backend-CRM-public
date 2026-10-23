using backend.Auth;
using backend.Domain.Products;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Products.Management;

public sealed partial class CatalogService
{
    private async Task<ProductDetailResponse> BuildDetailResponse(int id, CancellationToken cancellationToken)
        => await dbContext.Products
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
            .FirstAsync(cancellationToken);

    private static IQueryable<Product> ApplySorting(IQueryable<Product> query, string sortBy, string sortDir)
    {
        var descending = string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy switch
        {
            "code" => descending ? query.OrderByDescending(product => product.Code) : query.OrderBy(product => product.Code),
            "createdAt" => descending ? query.OrderByDescending(product => product.CreatedAt).ThenByDescending(product => product.Id) : query.OrderBy(product => product.CreatedAt).ThenBy(product => product.Id),
            _ => descending ? query.OrderByDescending(product => product.Name).ThenByDescending(product => product.Id) : query.OrderBy(product => product.Name).ThenBy(product => product.Id)
        };
    }

    private static string NormalizeSortField(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return "name";
        }

        var normalized = sortBy.Trim();
        return AllowedSortFields.FirstOrDefault(field => string.Equals(field, normalized, StringComparison.OrdinalIgnoreCase)) ?? "name";
    }

    private static string NormalizeSortDirection(string? sortDir)
        => string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

    private static Dictionary<string, string[]> ValidateCreateProductRequest(CreateProductRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            errors["code"] = ["Code is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            errors["type"] = ["Type is required."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidatePatchProductRequest(PatchProductRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (request.Code is not null && string.IsNullOrWhiteSpace(request.Code))
        {
            errors["code"] = ["Code cannot be empty."];
        }

        if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name cannot be empty."];
        }

        if (request.Type is not null && string.IsNullOrWhiteSpace(request.Type))
        {
            errors["type"] = ["Type cannot be empty."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateCreatePackageRequest(CreatePackageRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            errors["code"] = ["Code is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name is required."];
        }

        if (request.DurationDays is <= 0)
        {
            errors["durationDays"] = ["Duration days must be greater than 0 when provided."];
        }

        if (request.DiscordDays is <= 0)
        {
            errors["discordDays"] = ["Discord days must be greater than 0 when provided."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidatePatchPackageRequest(PatchPackageRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name cannot be empty."];
        }

        if (request.DurationDays is <= 0)
        {
            errors["durationDays"] = ["Duration days must be greater than 0 when provided."];
        }

        if (request.DiscordDays is <= 0)
        {
            errors["discordDays"] = ["Discord days must be greater than 0 when provided."];
        }

        return errors;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
