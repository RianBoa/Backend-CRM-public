using System.Text.Json;
using backend.Domain.Auditing;
using backend.Domain.RoadMaps;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;

namespace backend.Features.RoadMaps;

public sealed class StudentRoadMapService(ApplicationDbContext db, TimeProvider clock)
{
    public async Task<PagedResponse<StudentRoadMapItem>?> GetAsync(
        Ulid enrollmentId, StudentRoadMapQuery options, CancellationToken ct)
    {
        if (!await db.Enrollments.AnyAsync(x => x.Id == enrollmentId, ct)) return null;
        var query = db.RoadMapAssignments.AsNoTracking().Where(x => x.EnrollmentId == enrollmentId);
        if (options.TemplateId is { } templateId)
            query = query.Where(x => x.RoadMapItem.RoadMapTemplateId == templateId);
        if (options.Status is { } status)
        {
            var parsed = Enum.Parse<RoadMapAssignment.AssignmentStatus>(status);
            query = query.Where(x => x.Status == parsed);
        }
        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            var search = options.Search.Trim();
            query = query.Where(x => x.RoadMapItem.Title.Contains(search));
        }
        var total = await query.CountAsync(ct);
        var descending = options.SortDir == "desc";
        var ordered = options.SortBy == "title"
            ? descending ? query.OrderByDescending(x => x.RoadMapItem.Title) : query.OrderBy(x => x.RoadMapItem.Title)
            : descending ? query.OrderByDescending(x => x.Enrollment.EnrolledAt.AddDays(x.DueAtDaysFromEnrollment))
                : query.OrderBy(x => x.Enrollment.EnrolledAt.AddDays(x.DueAtDaysFromEnrollment));
        var rows = await ordered.ThenBy(x => x.Id).Skip((options.Page - 1) * options.PageSize).Take(options.PageSize)
            .Select(x => new { x.Id, x.RoadMapItemId, x.RoadMapItem.RoadMapTemplateId,
                x.RoadMapItem.Title, x.RoadMapItem.Type, x.Status, x.Enrollment.EnrolledAt, x.DueAtDaysFromEnrollment })
            .ToListAsync(ct);
        return new(rows.Select(x => new StudentRoadMapItem(x.Id.ToString(), x.RoadMapItemId, x.RoadMapTemplateId,
            x.Title, x.Type.ToString(), x.Status.ToString(), x.Status == RoadMapAssignment.AssignmentStatus.Completed,
            x.EnrolledAt.AddDays(x.DueAtDaysFromEnrollment))).ToList(), total, options.Page, options.PageSize);
    }

    public async Task<CompletionResult> SetCompletionAsync(
        Ulid enrollmentId, Ulid assignmentId, Ulid actorId, bool completed, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == actorId && x.IsActive, ct)) return new(null, "actor_forbidden");
        var assignment = await db.RoadMapAssignments.Include(x => x.Enrollment).Include(x => x.RoadMapItem)
            .FirstOrDefaultAsync(x => x.Id == assignmentId && x.EnrollmentId == enrollmentId, ct);
        if (assignment is null) return new(null, "roadmap_assignment_not_found");
        var previous = assignment.Status;
        bool changed;
        try { changed = assignment.SetCompletion(completed, clock.GetUtcNow().UtcDateTime); }
        catch (InvalidOperationException) { return new(null, "roadmap_assignment_skipped"); }
        try
        {
            if (changed)
            {
                db.AuditLogs.Add(AuditLog.Create(actorId, "set_completion", "roadmap_assignment", assignment.Id.ToString(),
                    JsonSerializer.Serialize(new { enrollmentId = enrollmentId.ToString(), previous = previous.ToString(),
                        status = assignment.Status.ToString() })));
                await db.SaveChangesAsync(ct);
            }
        }
        catch (DbUpdateConcurrencyException) { return new(null, "roadmap_concurrent_change"); }
        return new(new(assignment.Id.ToString(), assignment.RoadMapItemId, assignment.RoadMapItem.RoadMapTemplateId,
            assignment.RoadMapItem.Title, assignment.RoadMapItem.Type.ToString(), assignment.Status.ToString(),
            assignment.Status == RoadMapAssignment.AssignmentStatus.Completed,
            assignment.Enrollment.EnrolledAt.AddDays(assignment.DueAtDaysFromEnrollment)));
    }
}
