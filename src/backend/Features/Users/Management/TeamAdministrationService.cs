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

public sealed partial class TeamAdministrationService(ApplicationDbContext dbContext,
    IUserPasswordService passwordService)
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private static readonly string[] AllowedSortFields = ["fullName", "email", "role", "createdAt"];
}
