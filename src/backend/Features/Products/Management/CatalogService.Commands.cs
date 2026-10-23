using backend.Auth;
using backend.Domain.Products;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Products.Management;

public sealed partial class CatalogService
{
    public async Task<ModuleOutcome<ProductDetailResponse>> Create(CreateProductRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateCreateProductRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var normalizedCode = request.Code!.Trim();
        var normalizedLegacyCode = NormalizeOptional(request.LegacyCode);

        if (await dbContext.Products.AnyAsync(product => product.Code == normalizedCode, cancellationToken))
        {
            return Conflict(new ErrorResponse("product_code_conflict", "Product code already exists."));
        }

        if (!string.IsNullOrWhiteSpace(normalizedLegacyCode) &&
            await dbContext.Products.AnyAsync(product => product.LegacyCode == normalizedLegacyCode, cancellationToken))
        {
            return Conflict(new ErrorResponse("product_legacy_code_conflict", "Legacy code already exists."));
        }

        try
        {
            var product = Product.Create(
                normalizedCode,
                request.Name!.Trim(),
                request.Type!.Trim(),
                request.DiscordAccess,
                normalizedLegacyCode,
                request.CreatedAt);

            dbContext.Products.Add(product);
            await ModuleAudit.SaveGeneratedIdentityAsync(dbContext, actor.Id, "create", "product", () => product.Id.ToString(), () => new { product.Code, product.Name }, cancellationToken);

            var response = await BuildDetailResponse(product.Id, cancellationToken);
            return ModuleOutcome<ProductDetailResponse>.Success(response);
        }
        catch (ArgumentException ex)
        {
            return Invalid(ToErrorDictionary("request", ex.Message));
        }
    }

    public async Task<ModuleOutcome<ProductDetailResponse>> Patch(int id,
        PatchProductRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (request is null || !request.HasChanges())
        {
            return Invalid(ToErrorDictionary("body", "At least one field must be provided for patch."));
        }

        var errors = ValidatePatchProductRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var product = await dbContext.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            return Missing(new ErrorResponse("product_not_found", "Product was not found."));
        }

        var nextCode = request.Code is null ? product.Code : request.Code.Trim();
        var nextLegacyCode = request.LegacyCode is null ? product.LegacyCode : NormalizeOptional(request.LegacyCode);

        if (!string.Equals(nextCode, product.Code, StringComparison.Ordinal) &&
            await dbContext.Products.AnyAsync(x => x.Id != id && x.Code == nextCode, cancellationToken))
        {
            return Conflict(new ErrorResponse("product_code_conflict", "Product code already exists."));
        }

        if (!string.Equals(nextLegacyCode, product.LegacyCode, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(nextLegacyCode) &&
            await dbContext.Products.AnyAsync(x => x.Id != id && x.LegacyCode == nextLegacyCode, cancellationToken))
        {
            return Conflict(new ErrorResponse("product_legacy_code_conflict", "Legacy code already exists."));
        }

        dbContext.Entry(product).CurrentValues[nameof(Product.Code)] = nextCode;
        dbContext.Entry(product).CurrentValues[nameof(Product.LegacyCode)] = nextLegacyCode;
        dbContext.Entry(product).CurrentValues[nameof(Product.Name)] = request.Name?.Trim() ?? product.Name;
        dbContext.Entry(product).CurrentValues[nameof(Product.Type)] = request.Type?.Trim() ?? product.Type;
        dbContext.Entry(product).CurrentValues[nameof(Product.IsActive)] = request.IsActive ?? product.IsActive;
        dbContext.Entry(product).CurrentValues[nameof(Product.IsArchived)] = request.IsArchived ?? product.IsArchived;
        dbContext.Entry(product).CurrentValues[nameof(Product.DiscordAccess)] = request.DiscordAccess ?? product.DiscordAccess;

        await ModuleAudit.SaveAsync(dbContext, actor.Id, "patch", "product", product.Id.ToString(), new { request.Code, request.Name, request.Type }, cancellationToken);

        var response = await BuildDetailResponse(product.Id, cancellationToken);
        return ModuleOutcome<ProductDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<ProductDetailResponse>> AddPackage(int id,
        CreatePackageRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateCreatePackageRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var product = await dbContext.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            return Missing(new ErrorResponse("product_not_found", "Product was not found."));
        }

        var normalizedCode = request.Code!.Trim();
        if (await dbContext.Packages.AnyAsync(package => package.Code == normalizedCode, cancellationToken))
        {
            return Conflict(new ErrorResponse("package_code_conflict", "Package code already exists."));
        }

        try
        {
            var package = Package.Create(
                normalizedCode,
                request.Name!.Trim(),
                product,
                request.DurationDays,
                request.DiscordAccess,
                request.DiscordDays);

            dbContext.Packages.Add(package);
            await ModuleAudit.SaveGeneratedIdentityAsync(dbContext, actor.Id, "add_package", "product", () => product.Id.ToString(), () => new { package.Id, package.Code }, cancellationToken);

            var response = await BuildDetailResponse(product.Id, cancellationToken);
            return ModuleOutcome<ProductDetailResponse>.Success(response);
        }
        catch (ArgumentException ex)
        {
            return Invalid(ToErrorDictionary("request", ex.Message));
        }
    }

    public async Task<ModuleOutcome<PackageResponse>> PatchPackage(int packageId,
        PatchPackageRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (request is null || !request.HasChanges())
        {
            return Invalid(ToErrorDictionary("body", "At least one field must be provided for patch."));
        }

        var errors = ValidatePatchPackageRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var package = await dbContext.Packages.FirstOrDefaultAsync(x => x.Id == packageId, cancellationToken);
        if (package is null)
        {
            return Missing(new ErrorResponse("package_not_found", "Package was not found."));
        }

        dbContext.Entry(package).CurrentValues[nameof(Package.Name)] = request.Name?.Trim() ?? package.Name;
        dbContext.Entry(package).CurrentValues[nameof(Package.DurationDays)] = request.DurationDays ?? package.DurationDays;
        dbContext.Entry(package).CurrentValues[nameof(Package.DiscordAccess)] = request.DiscordAccess ?? package.DiscordAccess;
        dbContext.Entry(package).CurrentValues[nameof(Package.DiscordDays)] = request.DiscordDays ?? package.DiscordDays;

        await ModuleAudit.SaveAsync(dbContext, actor.Id, "patch_package", "package", package.Id.ToString(), new { request.Name, request.DurationDays }, cancellationToken);

        return ModuleOutcome<PackageResponse>.Success(new PackageResponse(
            package.Id,
            package.Code,
            request.Name?.Trim() ?? package.Name,
            request.DurationDays ?? package.DurationDays,
            request.DiscordAccess ?? package.DiscordAccess,
            request.DiscordDays ?? package.DiscordDays));
    }
}
