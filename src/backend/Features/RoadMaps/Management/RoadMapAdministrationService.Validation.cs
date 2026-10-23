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
    private static Dictionary<string, string[]> ValidateQuery(RoadMapTemplateListQuery query)
        => new ListValidation()
            .Paging(query.Page, query.PageSize)
            .Text(query.Search, "search")
            .Positive(query.ProductId, "productId")
            .Errors;

    private static Dictionary<string, string[]> ValidateQuery(RoadMapAssignmentListQuery query)
        => new ListValidation()
            .Paging(query.Page, query.PageSize)
            .Identifier(query.EnrollmentId, "enrollmentId")
            .Identifier(query.UserId, "userId")
            .EnumValue<RoadMapAssignment.AssignmentStatus>(query.Status, "status")
            .Errors;
}
