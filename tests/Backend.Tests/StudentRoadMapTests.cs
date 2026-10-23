using backend.Domain.Clients;
using backend.Domain.LearningStructure;
using backend.Domain.Orders;
using backend.Domain.Products;
using backend.Domain.RoadMaps;
using backend.Domain.Users;
using backend.Features.RoadMaps;
using backend.Features.RoadMaps.Management;
using backend.Features.Common;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using Xunit;

namespace Backend.Tests;

public sealed class StudentRoadMapTests
{
    [Fact]
    public async Task CompletionAndAuditAreSavedAndRepeatedRequestIsNoOp()
    {
        await using var db = TestDatabase.Create();
        var (actor, assignment) = await Seed(db);
        var service = new StudentRoadMapService(db, TimeProvider.System);
        var first = await service.SetCompletionAsync(assignment.EnrollmentId, assignment.Id, actor.Id, true, default);
        var second = await service.SetCompletionAsync(assignment.EnrollmentId, assignment.Id, actor.Id, true, default);
        Assert.True(first.Item!.Completed);
        Assert.Null(second.ErrorCode);
        Assert.Single(await db.AuditLogs.ToListAsync());
        Assert.Equal(actor.Id, (await db.AuditLogs.SingleAsync()).UserId);
    }

    [Fact]
    public async Task WrongEnrollmentCannotChangeAssignment()
    {
        await using var db = TestDatabase.Create();
        var (actor, assignment) = await Seed(db);
        var result = await new StudentRoadMapService(db, TimeProvider.System)
            .SetCompletionAsync(Ulid.NewUlid(), assignment.Id, actor.Id, true, default);
        Assert.Equal("roadmap_assignment_not_found", result.ErrorCode);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task InactiveActorCannotComplete()
    {
        await using var db = TestDatabase.Create();
        var (actor, assignment) = await Seed(db);
        actor.Deactivate();
        await db.SaveChangesAsync();
        var result = await new StudentRoadMapService(db, TimeProvider.System)
            .SetCompletionAsync(assignment.EnrollmentId, assignment.Id, actor.Id, true, default);
        Assert.Equal("actor_forbidden", result.ErrorCode);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ChecklistIsPagedAndScoped()
    {
        await using var db = TestDatabase.Create();
        var (_, assignment) = await Seed(db);
        await Seed(db);
        var result = await new StudentRoadMapService(db, TimeProvider.System)
            .GetAsync(assignment.EnrollmentId, new() { PageSize = 1 }, default);
        Assert.Equal(1, result!.Total);
        Assert.Equal(assignment.Id.ToString(), Assert.Single(result.Items).AssignmentId);
    }

    [Theory]
    [InlineData(-1, "Overdue")]
    [InlineData(31, "Pending")]
    public async Task UncheckingRecomputesDueStatus(int daysFromNow, string expected)
    {
        await using var db = TestDatabase.Create();
        var (_, assignment) = await Seed(db);
        assignment.MarkAsCompleted();
        Assert.True(assignment.SetCompletion(false, assignment.Enrollment.EnrolledAt.AddDays(-daysFromNow)));
        Assert.Equal(expected, assignment.Status.ToString());
    }

    [Fact]
    public async Task SkippedAssignmentRequiresExplicitRestore()
    {
        await using var db = TestDatabase.Create();
        var (actor, assignment) = await Seed(db);
        assignment.UpdateStatus(RoadMapAssignment.AssignmentStatus.Skipped);
        await db.SaveChangesAsync();
        var result = await new StudentRoadMapService(db, TimeProvider.System)
            .SetCompletionAsync(assignment.EnrollmentId, assignment.Id, actor.Id, true, default);
        Assert.Equal("roadmap_assignment_skipped", result.ErrorCode);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task AnotherCoursesChecklistIsRejectedBeforeAssignmentAndAudit()
    {
        await using var db = TestDatabase.Create();
        var (actor, existing) = await Seed(db);
        var otherProduct = Product.Create("other-course", "Other", "Course", false);
        db.Add(otherProduct);
        await db.SaveChangesAsync();
        var template = RoadMapTemplate.Create("Other checklist", otherProduct.Id);
        db.Add(template);
        await db.SaveChangesAsync();
        var item = RoadMapItem.Create(template.Id, "Other lesson", RoadMapItem.RoadMapItemType.Lesson, 0);
        db.Add(item);
        await db.SaveChangesAsync();

        var result = await new RoadMapAdministrationService(db).CreateAssignment(
            new CreateRoadMapAssignmentRequest(item.Id, existing.EnrollmentId.ToString(), 0, actor.Id.ToString()),
            default, new ModuleActor(actor.Id, false));

        Assert.Equal("roadmap_product_mismatch", result.Error!.Code);
        Assert.Equal(1, await db.RoadMapAssignments.CountAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
        Assert.Throws<ArgumentException>(() => RoadMapAssignment.Create(item.Id, item,
            existing.EnrollmentId, existing.Enrollment, 0, actor.Id, actor));
    }

    [Fact]
    public async Task CompatibleChecklistAssignmentIsAccepted()
    {
        await using var db = TestDatabase.Create();
        var (actor, existing) = await Seed(db);
        var item = RoadMapItem.Create(existing.RoadMapItem.RoadMapTemplateId,
            "Next lesson", RoadMapItem.RoadMapItemType.Lesson, 1);
        db.Add(item);
        await db.SaveChangesAsync();

        var result = await new RoadMapAdministrationService(db).CreateAssignment(
            new CreateRoadMapAssignmentRequest(item.Id, existing.EnrollmentId.ToString(), 1, actor.Id.ToString()),
            default, new ModuleActor(actor.Id, false));

        Assert.Null(result.Error);
        Assert.Equal(2, await db.RoadMapAssignments.CountAsync());
        Assert.Single(await db.AuditLogs.ToListAsync());
    }

    private static async Task<(User, RoadMapAssignment)> Seed(ApplicationDbContext db)
    {
        var actor = User.Create($"{Ulid.NewUlid()}@example.test", "@curator", "test-hash", User.UserRole.Curator, "Curator");
        var product = Product.Create(Ulid.NewUlid().ToString(), "Course", "Course", false);
        var package = Package.Create("standard", "Standard", product);
        db.AddRange(actor, product, package);
        await db.SaveChangesAsync();
        var client = Client.Create("Student", Email.From($"{Ulid.NewUlid()}@example.test"),
            ContactInfo.Create("123456789", null, null, null), new Financials(0, 0));
        db.Clients.Add(client);
        var purchase = OrderItem.Create(Order.Create(client.Id, OrderSource.Manual), product, package, 100, 1);
        var enrollment = Enrollment.Create(purchase);
        var template = RoadMapTemplate.Create("Achievements", product.Id);
        db.AddRange(enrollment, template);
        await db.SaveChangesAsync();
        var item = RoadMapItem.Create(template.Id, "First case", RoadMapItem.RoadMapItemType.Case, 0);
        db.Add(item);
        await db.SaveChangesAsync();
        var assignment = RoadMapAssignment.Create(item.Id, item, enrollment.Id, enrollment, 0, actor.Id, actor);
        db.Add(assignment);
        await db.SaveChangesAsync();
        return (actor, assignment);
    }
}
