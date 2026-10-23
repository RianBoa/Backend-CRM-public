using backend.Domain.LearningStructure;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Learning;

public sealed class LearningDirectory(ApplicationDbContext db)
{
    public async Task<(IReadOnlyList<CohortCard> Items, int Total)> GetCohortsAsync(
        LearningDirectoryQuery request, CancellationToken cancellationToken)
    {
        var query = db.Cohorts.AsNoTracking().AsQueryable();
        if (request.ActiveOnly) query = query.Where(x => x.Status == Cohort.CohortStatus.Active);
        if (request.ProductId is not null) query = query.Where(x => x.ProductId == request.ProductId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.StartAt).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new { x.Id, x.Name, x.ProductId, x.Status, x.StartAt, x.EndsAt,
                GroupsCount = x.Groups.Count, StudentsCount = x.Enrollments.Count })
            .ToListAsync(cancellationToken);
        return (rows.Select(x => new CohortCard(x.Id.ToString(), x.Name, x.ProductId,
            x.Status.ToString(), x.StartAt, x.EndsAt, x.GroupsCount, x.StudentsCount)).ToList(), total);
    }

    public async Task<(IReadOnlyList<GroupCard> Items, int Total)> GetGroupsAsync(
        LearningDirectoryQuery request, CancellationToken cancellationToken)
    {
        var query = db.Groups.AsNoTracking().AsQueryable();
        if (request.ActiveOnly) query = query.Where(x => x.Cohort.Status == Cohort.CohortStatus.Active);
        if (request.ProductId is not null) query = query.Where(x => x.Cohort.ProductId == request.ProductId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new { x.Id, x.Name, x.CohortId, CohortName = x.Cohort.Name,
                x.Cohort.ProductId, x.PackageId, PackageName = x.Package != null ? x.Package.Name : null,
                CohortStatus = x.Cohort.Status, StudentsCount = x.Enrollments.Count })
            .ToListAsync(cancellationToken);
        return (rows.Select(x => new GroupCard(x.Id.ToString(), x.Name, x.CohortId.ToString(),
            x.CohortName, x.ProductId, x.PackageId, x.PackageName, x.CohortStatus.ToString(),
            x.StudentsCount)).ToList(), total);
    }
}
