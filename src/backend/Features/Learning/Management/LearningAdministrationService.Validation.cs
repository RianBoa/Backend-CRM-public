using backend.Auth;
using backend.Domain.LearningStructure;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Learning.Management;

public sealed partial class LearningAdministrationService
{
    private static Dictionary<string, string[]> ValidateQuery(CohortListQuery query)
        => new ListValidation()
            .Paging(query.Page, query.PageSize)
            .Text(query.Search, "search")
            .Positive(query.ProductId, "productId")
            .EnumValue<Cohort.CohortStatus>(query.Status, "status")
            .Errors;

    private static Dictionary<string, string[]> ValidateQuery(GroupListQuery query)
        => new ListValidation()
            .Paging(query.Page, query.PageSize)
            .Text(query.Search, "search")
            .Identifier(query.CohortId, "cohortId")
            .Positive(query.PackageId, "packageId")
            .Identifier(query.MentorId, "mentorId")
            .Errors;

    private static Dictionary<string, string[]> ValidateQuery(EnrollmentListQuery query)
        => new ListValidation()
            .Paging(query.Page, query.PageSize)
            .Identifier(query.OrderId, "orderId")
            .Identifier(query.CohortId, "cohortId")
            .Identifier(query.GroupId, "groupId")
            .EnumValue<Enrollment.EnrollmentStatus>(query.Status, "status")
            .Errors;
}
