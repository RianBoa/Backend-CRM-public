using backend.Auth;
using backend.Domain.Cases;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Cases.Management;

public sealed partial class CaseManagementService(ApplicationDbContext dbContext)
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private static readonly string[] AllowedSortFields = ["createdAt", "amount", "status"];
}
