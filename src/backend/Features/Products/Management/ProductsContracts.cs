using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using backend.Features.Common;
namespace backend.Features.Products.Management;

public sealed record ProductListQuery(
    int? Page,
    int? PageSize,
    string? Search,
    string? SortBy,
    string? SortDir,
    string? Type,
    bool? IsActive,
    bool? IsArchived);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateProductRequest(
    [StringLength(64), NotBlank] string? Code,
    [StringLength(64)] string? LegacyCode,
    [StringLength(200), NotBlank] string? Name,
    [StringLength(100), NotBlank] string? Type,
    bool DiscordAccess,
    DateOnly? CreatedAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatchProductRequest(
    [StringLength(64), NotBlank] string? Code,
    [StringLength(64)] string? LegacyCode,
    [StringLength(200), NotBlank] string? Name,
    [StringLength(100), NotBlank] string? Type,
    bool? IsActive,
    bool? IsArchived,
    bool? DiscordAccess)
{
    public bool HasChanges()
        => Code is not null ||
           LegacyCode is not null ||
           Name is not null ||
           Type is not null ||
           IsActive is not null ||
           IsArchived is not null ||
           DiscordAccess is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreatePackageRequest([StringLength(64), NotBlank] string? Code, [StringLength(200), NotBlank] string? Name, [Range(1, 36500)] int? DurationDays, bool DiscordAccess, [Range(1, 36500)] int? DiscordDays);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatchPackageRequest([StringLength(200), NotBlank] string? Name, [Range(1, 36500)] int? DurationDays, bool? DiscordAccess, [Range(1, 36500)] int? DiscordDays)
{
    public bool HasChanges()
        => Name is not null ||
           DurationDays is not null ||
           DiscordAccess is not null ||
           DiscordDays is not null;
}
public sealed record ProductListItemResponse(int Id, string Code, string? LegacyCode, string Name, string Type, bool IsActive, bool IsArchived, bool DiscordAccess, DateOnly CreatedAt, int PackagesCount);
public sealed record ProductSelectItemResponse(int Value, string Label, string Code, string Type);
public sealed record ProductDetailResponse(int Id, string Code, string? LegacyCode, string Name, string Type, bool IsActive, bool IsArchived, bool DiscordAccess, DateOnly CreatedAt, IReadOnlyList<PackageResponse> Packages);
public sealed record PackageResponse(int Id, string Code, string Name, int? DurationDays, bool DiscordAccess, int? DiscordDays);
