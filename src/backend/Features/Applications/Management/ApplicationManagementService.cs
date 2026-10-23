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

public sealed partial class ApplicationManagementService(ApplicationDbContext dbContext)
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private static readonly string[] AllowedSortFields = ["createdAt", "status", "reason"];
}
