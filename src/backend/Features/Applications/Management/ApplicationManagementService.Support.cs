using backend.Auth;
using backend.Domain.Auditing;
using System.Text.Json;
using backend.Domain.Applications;
using backend.Domain.Users;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Applications.Management;

public sealed partial class ApplicationManagementService
{
    private IQueryable<ApplicationDetailResponse> BuildDetailQuery(Ulid applicationId)
        => dbContext.Applications
            .AsNoTracking()
            .Where(application => application.Id == applicationId)
            .Select(application => new ApplicationDetailResponse(
                application.Id.ToString(),
                application.TypeId.ToString(),
                application.Type.Name,
                application.AuthorId.ToString(),
                application.Author.FullName,
                application.ClientId.HasValue ? application.ClientId.Value.ToString() : null,
                application.Client != null ? application.Client.Name : null,
                application.OrderId.HasValue ? application.OrderId.Value.ToString() : null,
                application.Title,
                application.Description,
                application.Reason.ToString(),
                application.Status.ToString(),
                application.CreatedAt,
                application.ProcessedAt));

    private static IQueryable<Application> ApplySorting(IQueryable<Application> query, string sortBy, string sortDir)
    {
        var descending = string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        return sortBy switch
        {
            "status" => descending ? query.OrderByDescending(application => application.Status).ThenByDescending(application => application.Id) : query.OrderBy(application => application.Status).ThenBy(application => application.Id),
            "reason" => descending ? query.OrderByDescending(application => application.Reason).ThenByDescending(application => application.Id) : query.OrderBy(application => application.Reason).ThenBy(application => application.Id),
            _ => descending ? query.OrderByDescending(application => application.CreatedAt).ThenByDescending(application => application.Id) : query.OrderBy(application => application.CreatedAt).ThenBy(application => application.Id)
        };
    }

    private static string NormalizeSortField(string? sortBy)
        => string.IsNullOrWhiteSpace(sortBy) ? "createdAt" : (AllowedSortFields.FirstOrDefault(field => string.Equals(field, sortBy.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "createdAt");

    private static string NormalizeSortDirection(string? sortDir)
        => string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

    private static bool TryParseUlid(string? value, out Ulid ulid)
        => Ulid.TryParse(value?.Trim(), out ulid);

    private static Dictionary<string, string[]> ValidateCreateApplicationRequest(CreateApplicationRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.TypeId))
        {
            errors["typeId"] = ["Type id is required."];
        }

        if (string.IsNullOrWhiteSpace(request.AuthorId))
        {
            errors["authorId"] = ["Author id is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["title"] = ["Title is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            errors["reason"] = ["Reason is required."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateCreateApplicationTypeRequest(CreateApplicationTypeRequest? request)
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

        if (string.IsNullOrWhiteSpace(request.TargetRole))
        {
            errors["targetRole"] = ["Target role is required."];
        }

        return errors;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
