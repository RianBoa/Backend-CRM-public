using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using backend.Features.Common;
namespace backend.Features.Cases.Management;

public sealed record CaseListQuery(
    int? Page,
    int? PageSize,
    string? SortBy,
    string? SortDir,
    string? ClientId,
    string? CreatedById,
    string? Status,
    string? Type,
    bool? IsFeatured,
    bool? HasConsent,
    DateTime? CreatedFrom,
    DateTime? CreatedTo);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCaseRequest(
    string? ClientId,
    string? CreatedById,
    [NotBlank] string? Type,
    [Range(typeof(decimal), "0.01", "9999999999.99")] decimal Amount,
    [StringLength(10), NotBlank] string? Currency,
    [StringLength(500)] string? ScreenshotUrl,
    [StringLength(500)] string? TelegramUrl,
    [StringLength(1000)] string? Comment,
    bool? IsFeatured,
    bool? HasConsent);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeCaseStatusRequest([NotBlank] string? Status);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeCaseFeaturedRequest(bool? IsFeatured);

public sealed record CaseListItemResponse(
    string Id,
    string ClientId,
    string ClientName,
    string CreatedById,
    string CreatedByName,
    string Type,
    decimal? Amount,
    string Currency,
    string Status,
    bool IsFeatured,
    bool HasConsent,
    DateTime CreatedAt);

public sealed record CaseDetailResponse(
    string Id,
    string ClientId,
    string ClientName,
    string CreatedById,
    string CreatedByName,
    string Type,
    decimal? Amount,
    string Currency,
    string? ScreenshotUrl,
    string? TelegramUrl,
    string? Comment,
    bool IsFeatured,
    bool HasConsent,
    string Status,
    DateTime CreatedAt);
