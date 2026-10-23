using backend.Auth;
using backend.Domain.LearningStructure;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Learning.Management;

public sealed partial class LearningAdministrationService(ApplicationDbContext dbContext)
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
}
