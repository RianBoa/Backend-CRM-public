using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using backend.Features.Common;
namespace backend.Features.Applications.Management;

public sealed record ApplicationListQuery(
    int? Page,
    int? PageSize,
    string? Search,
    string? SortBy,
    string? SortDir,
    string? Status,
    string? Reason,
    string? TypeId,
    string? AuthorId,
    string? ClientId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateApplicationRequest(
    string? TypeId,
    string? AuthorId,
    string? ClientId,
    string? OrderId,
    [StringLength(200), NotBlank] string? Title,
    [StringLength(2000)] string? Description,
    string? Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeApplicationStatusRequest([NotBlank] string? Status);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateApplicationTypeRequest([StringLength(50), NotBlank] string? Code, [StringLength(100), NotBlank] string? Name, string? TargetRole, int? SlaDays);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeApplicationTypeStatusRequest(bool? IsActive);

public sealed record ApplicationListItemResponse(
    string Id,
    string TypeId,
    string TypeName,
    string AuthorId,
    string AuthorName,
    string? ClientId,
    string? ClientName,
    string? OrderId,
    string Title,
    string Reason,
    string Status,
    DateTime CreatedAt,
    DateTime? ProcessedAt);

public sealed record ApplicationDetailResponse(
    string Id,
    string TypeId,
    string TypeName,
    string AuthorId,
    string AuthorName,
    string? ClientId,
    string? ClientName,
    string? OrderId,
    string Title,
    string? Description,
    string Reason,
    string Status,
    DateTime CreatedAt,
    DateTime? ProcessedAt);

public sealed record ApplicationTypeItemResponse(string Id, string Code, string Name, string TargetRole, int? SlaDays, bool IsActive);
