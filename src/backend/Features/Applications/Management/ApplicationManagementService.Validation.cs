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
    private static Dictionary<string, string[]> ValidateQuery(ApplicationListQuery query)
        => new ListValidation()
            .Paging(query.Page, query.PageSize)
            .Sorting(query.SortBy, query.SortDir, AllowedSortFields)
            .Text(query.Search, "search")
            .EnumValue<Application.ApplicationStatus>(query.Status, "status")
            .EnumValue<Application.ApplicationReason>(query.Reason, "reason")
            .Identifier(query.TypeId, "typeId")
            .Identifier(query.AuthorId, "authorId")
            .Identifier(query.ClientId, "clientId")
            .Errors;
}
