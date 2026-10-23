using backend.Auth;
using backend.Domain.Applications;
using backend.Domain.RoadMaps;
using backend.Domain.Users;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using DomainUser = backend.Domain.Users.User;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Users.Management;

public sealed partial class TeamAdministrationService
{
    private static Dictionary<string, string[]> ValidateQuery(UserListQuery query)
        => new ListValidation()
            .Paging(query.Page, query.PageSize)
            .Sorting(query.SortBy, query.SortDir, AllowedSortFields)
            .Text(query.Search, "search")
            .EnumValue<DomainUser.UserRole>(query.Role, "role")
            .Errors;
}
