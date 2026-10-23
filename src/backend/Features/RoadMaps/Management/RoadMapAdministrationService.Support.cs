using backend.Auth;
using backend.Domain.RoadMaps;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.RoadMaps.Management;

public sealed partial class RoadMapAdministrationService
{
    private IQueryable<RoadMapTemplateDetailResponse> BuildTemplateDetail(int templateId)
        => dbContext.RoadMapTemplates
            .AsNoTracking()
            .Where(x => x.Id == templateId)
            .Select(template => new RoadMapTemplateDetailResponse(
                template.Id,
                template.Name,
                template.Description,
                template.ProductId,
                template.Product.Name,
                template.IsActive,
                dbContext.RoadMapItems
                    .Where(item => item.RoadMapTemplateId == template.Id)
                    .OrderBy(item => item.DaysFromStart)
                    .ThenBy(item => item.Title)
                    .Select(item => new RoadMapItemResponse(
                        item.Id,
                        item.Title,
                        item.Type.ToString(),
                        item.DaysFromStart))
                    .ToList()));

    private static Dictionary<string, string[]> ValidateCreateTemplateRequest(CreateRoadMapTemplateRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (request.ProductId <= 0)
        {
            errors["productId"] = ["ProductId is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name is required."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateCreateItemRequest(CreateRoadMapItemRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["title"] = ["Title is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            errors["type"] = ["Type is required."];
        }

        if (request.DaysFromStart < 0)
        {
            errors["daysFromStart"] = ["DaysFromStart must be non-negative."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateCreateAssignmentRequest(CreateRoadMapAssignmentRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.EnrollmentId))
        {
            errors["enrollmentId"] = ["EnrollmentId is required."];
        }

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            errors["userId"] = ["UserId is required."];
        }

        if (request.RoadMapItemId <= 0)
        {
            errors["roadMapItemId"] = ["RoadMapItemId is required."];
        }

        if (request.DueAtDaysFromEnrollment < 0)
        {
            errors["dueAtDaysFromEnrollment"] = ["DueAtDaysFromEnrollment must be non-negative."];
        }

        return errors;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
