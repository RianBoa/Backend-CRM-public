using backend.Auth;
using backend.Domain.RoadMaps;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.RoadMaps.Management;

public sealed partial class RoadMapAdministrationService(ApplicationDbContext dbContext)
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
}
