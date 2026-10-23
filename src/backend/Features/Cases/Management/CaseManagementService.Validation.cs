using backend.Auth;
using backend.Domain.Cases;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Cases.Management;

public sealed partial class CaseManagementService
{
    private static Dictionary<string, string[]> ValidateQuery(CaseListQuery query)
        => new ListValidation()
            .Paging(query.Page, query.PageSize)
            .Sorting(query.SortBy, query.SortDir, AllowedSortFields)
            .Identifier(query.ClientId, "clientId")
            .Identifier(query.CreatedById, "createdById")
            .EnumValue<Case.CaseStatus>(query.Status, "status")
            .EnumValue<Case.CaseType>(query.Type, "type")
            .Dates(query.CreatedFrom, query.CreatedTo)
            .Errors;
}
