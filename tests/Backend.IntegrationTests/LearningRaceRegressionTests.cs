using System.Net;
using System.Net.Http.Json;
using backend.Domain.Auditing;
using backend.Domain.LearningStructure;
using backend.Domain.Products;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUlid;
using Xunit;

namespace Backend.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class LearningRaceRegressionTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevocationWinsOverAStaleFreezeOrTransferWithoutLosingAuditRollback(bool transfer)
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        var setup = await SeedLearningAsync(database, scenario, assigned: true);
        var gate = new SaveGate(db => db.ChangeTracker.Entries<Enrollment>().Any(x => x.State == EntityState.Modified));
        using var loser = new ApiHost(database, configure: s => s.AddScoped(_ => database.CreateContext(gate)));
        using var winner = new ApiHost(database);
        await loser.AuthenticateAsync(database, scenario.Admin);
        await winner.AuthenticateAsync(database, scenario.Admin);
        var pending = transfer
            ? loser.Client.PostAsJsonAsync($"/learning/enrollments/{setup.Enrollment}/assign-group", new { groupId = setup.OtherGroup.ToString() })
            : loser.Client.PostAsJsonAsync($"/learning/enrollments/{setup.Enrollment}/status", new { status = "Frozen" });
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.OK, (await winner.Client.PostAsJsonAsync(
                $"/learning/enrollments/{setup.Enrollment}/status", new { status = "AccessRevoked" })).StatusCode);
        }
        finally { gate.Release.TrySetResult(); }
        Assert.Equal(HttpStatusCode.Conflict, (await pending).StatusCode);
        await using var read = database.CreateContext();
        var enrollment = await read.Enrollments.SingleAsync();
        Assert.Equal(Enrollment.EnrollmentStatus.AccessRevoked, enrollment.Status);
        Assert.Equal(setup.Group, enrollment.GroupId);
        Assert.Single(await read.AuditLogs.Where(x => x.EntityType == "enrollment").ToListAsync());
        Assert.Empty(await read.AuditLogs.Where(x => x.Action == "assign_group").ToListAsync());
    }

    [Fact]
    public async Task VersionRejectsIndependentContextUpdatesEvenWithoutChangingTheSameColumn()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        var setup = await SeedLearningAsync(database, scenario, assigned: true);
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var transfer = await LoadAsync(first, setup.Enrollment);
        var frozen = await LoadAsync(second, setup.Enrollment);
        var other = await first.Groups.Include(x => x.Cohort).SingleAsync(x => x.Id == setup.OtherGroup);
        transfer.AssignToGroup(other);
        first.AuditLogs.Add(AuditLog.Create(scenario.Admin.Id, "winning_transfer", "enrollment", setup.Enrollment.ToString()));
        await first.SaveChangesAsync();
        frozen.ChangeStatus(Enrollment.EnrollmentStatus.Frozen);
        second.AuditLogs.Add(AuditLog.Create(scenario.Admin.Id, "losing_freeze", "enrollment", setup.Enrollment.ToString()));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        Assert.Empty(second.ChangeTracker.Entries());
        await using var read = database.CreateContext();
        Assert.Equal(setup.OtherGroup, (await read.Enrollments.SingleAsync()).GroupId);
        Assert.Equal(Enrollment.EnrollmentStatus.Active, (await read.Enrollments.SingleAsync()).Status);
        Assert.Single(await read.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AssignmentAndPackageEditCannotBothCommitInEitherOrder(bool packageEditIsStale)
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        var setup = await SeedLearningAsync(database, scenario, assigned: false);
        int alternateId;
        await using (var db = database.CreateContext())
        {
            var product = await db.Products.SingleAsync();
            var alternate = Package.Create("alternate", "Alternate", product, 30);
            db.Packages.Add(alternate);
            await db.SaveChangesAsync();
            alternateId = alternate.Id;
        }
        var gate = new SaveGate(db => packageEditIsStale
            ? db.ChangeTracker.Entries<Group>().Any(x => x.State == EntityState.Modified)
            : db.ChangeTracker.Entries<Enrollment>().Any(x => x.State == EntityState.Modified));
        using var stale = new ApiHost(database, configure: s => s.AddScoped(_ => database.CreateContext(gate)));
        using var winner = new ApiHost(database);
        await stale.AuthenticateAsync(database, scenario.Admin);
        await winner.AuthenticateAsync(database, scenario.Admin);
        var groupUrl = $"/learning-structure/groups/{setup.Group}";
        var assignUrl = $"/learning/enrollments/{setup.Enrollment}/assign-group";
        var pending = packageEditIsStale
            ? stale.Client.PatchAsJsonAsync(groupUrl, new { packageId = alternateId })
            : stale.Client.PostAsJsonAsync(assignUrl, new { groupId = setup.Group.ToString() });
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var response = packageEditIsStale
                ? await winner.Client.PostAsJsonAsync(assignUrl, new { groupId = setup.Group.ToString() })
                : await winner.Client.PatchAsJsonAsync(groupUrl, new { packageId = alternateId });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally { gate.Release.TrySetResult(); }
        Assert.Equal(HttpStatusCode.Conflict, (await pending).StatusCode);
        await using var read = database.CreateContext();
        var group = await read.Groups.SingleAsync(x => x.Id == setup.Group);
        var enrollment = await read.Enrollments.SingleAsync();
        if (packageEditIsStale)
        {
            Assert.Equal(scenario.Package.Id, group.PackageId);
            Assert.Equal(group.Id, enrollment.GroupId);
            Assert.Single(await read.AuditLogs.Where(x => x.Action == "assign_group").ToListAsync());
            Assert.Empty(await read.AuditLogs.Where(x => x.EntityType == "group").ToListAsync());
        }
        else
        {
            Assert.Equal(alternateId, group.PackageId);
            Assert.Null(enrollment.GroupId);
            Assert.Empty(await read.AuditLogs.Where(x => x.EntityType == "enrollment").ToListAsync());
            Assert.Single(await read.AuditLogs.Where(x => x.EntityType == "group").ToListAsync());
        }
    }

    private static Task<Enrollment> LoadAsync(ApplicationDbContext db, Ulid id)
        => db.Enrollments.Include(x => x.OrderItem!).ThenInclude(x => x.Order).SingleAsync(x => x.Id == id);

    private static async Task<(Ulid Enrollment, Ulid Group, Ulid OtherGroup)> SeedLearningAsync(
        PostgresDatabase database, Scenario scenario, bool assigned)
    {
        await using var db = database.CreateContext();
        var purchase = await db.OrderItems.Include(x => x.Order).SingleAsync();
        var cohort = Cohort.Create(scenario.Product.Id, "Cohort", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30));
        var group = Group.Create(cohort, "Group A", packageId: scenario.Package.Id);
        var other = Group.Create(cohort, "Group B", packageId: scenario.Package.Id);
        var enrollment = Enrollment.Create(purchase);
        if (assigned) enrollment.AssignToGroup(group);
        db.AddRange(cohort, group, other, enrollment);
        await db.SaveChangesAsync();
        return (enrollment.Id, group.Id, other.Id);
    }
}
