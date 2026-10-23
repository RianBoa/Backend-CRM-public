using System.Text.Json;
using backend.Domain.Auditing;
using backend.Domain.LearningStructure;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;

namespace backend.Features.Learning;

public sealed class EnrollmentService(ApplicationDbContext db)
{
    public async Task<(IReadOnlyList<EnrollmentView> Items, int Total)> GetQueueAsync(
        AssignmentQueueQuery request, CancellationToken cancellationToken)
    {
        var query = db.Enrollments.AsNoTracking()
            .Where(x => x.Status == Enrollment.EnrollmentStatus.Pending && x.GroupId == null);
        if (request.ProductId is not null)
            query = query.Where(x => x.OrderItem != null && x.OrderItem.ProductId == request.ProductId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Order.Client.Name, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await WithContext(query.OrderBy(x => x.EnrolledAt).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize))
            .ToListAsync(cancellationToken);
        return (rows.Select(ToView).ToList(), total);
    }

    public async Task<EnrollmentView?> GetAsync(Ulid id, CancellationToken cancellationToken)
    {
        var enrollment = await WithContext(db.Enrollments.AsNoTracking())
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return enrollment is null ? null : ToView(enrollment);
    }

    public async Task<LearningChange> AssignAsync(Ulid id, Ulid groupId, Ulid? actorId, CancellationToken cancellationToken)
    {
        // The group-package check and enrollment write share a serializable transaction
        // Group edits use the same boundary so neither side can validate stale compatibility
        await using var write = await LearningWriteScope.BeginAsync(db, cancellationToken);
        var enrollment = await WithContext(db.Enrollments).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (enrollment is null) return LearningChange.Missing("enrollment_not_found", "Enrollment was not found.");
        var group = await db.Groups.Include(x => x.Cohort).FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken);
        if (group is null) return LearningChange.Missing("group_not_found", "Group was not found.");

        var previousGroupId = enrollment.GroupId;
        var previousStatus = enrollment.Status;
        try { enrollment.AssignToGroup(group); }
        catch (InvalidOperationException ex) { return LearningChange.Conflict(ex.Message); }

        if (previousGroupId != enrollment.GroupId || previousStatus != enrollment.Status)
        {
            db.AuditLogs.Add(AuditLog.Create(actorId, "assign_group", "enrollment", id.ToString(),
                JsonSerializer.Serialize(new { previousGroupId, groupId, clientId = enrollment.Order.ClientId })));
            await db.SaveChangesAsync(cancellationToken);
        }
        await write.CommitAsync(cancellationToken);
        return new(ToView(enrollment), null);
    }

    public async Task<LearningChange> ChangeStatusAsync(
        Ulid id, Enrollment.EnrollmentStatus status, Ulid? actorId, CancellationToken cancellationToken)
    {
        await using var write = await LearningWriteScope.BeginAsync(db, cancellationToken);
        var enrollment = await WithContext(db.Enrollments).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (enrollment is null) return LearningChange.Missing("enrollment_not_found", "Enrollment was not found.");
        var previousStatus = enrollment.Status;
        try { enrollment.ChangeStatus(status); }
        catch (InvalidOperationException ex) { return LearningChange.Conflict(ex.Message); }

        if (previousStatus != status)
        {
            db.AuditLogs.Add(AuditLog.Create(actorId, "change_learning_status", "enrollment", id.ToString(),
                JsonSerializer.Serialize(new { before = previousStatus.ToString(), after = status.ToString(), clientId = enrollment.Order.ClientId })));
            await db.SaveChangesAsync(cancellationToken);
        }
        await write.CommitAsync(cancellationToken);
        return new(ToView(enrollment), null);
    }

    private static IQueryable<Enrollment> WithContext(IQueryable<Enrollment> query) => query
        .Include(x => x.Order).ThenInclude(x => x.Client)
        .Include(x => x.OrderItem!).ThenInclude(x => x.Product)
        .Include(x => x.OrderItem!).ThenInclude(x => x.Package)
        .Include(x => x.Cohort).Include(x => x.Group);

    private static EnrollmentView ToView(Enrollment x) => new(
        x.Id.ToString(), x.Order.ClientId.ToString(), x.Order.Client.Name, x.OrderId.ToString(),
        x.OrderItemId?.ToString(), x.OrderItem?.ProductId, x.OrderItem?.Product.Name, x.OrderItem?.Package.Name,
        x.CohortId?.ToString(), x.Cohort?.Name, x.GroupId?.ToString(), x.Group?.Name,
        x.Status.ToString(), x.EnrolledAt, x.Cohort?.StartAt, x.Cohort?.EndsAt);
}
