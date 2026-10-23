using backend.Domain.LearningStructure;
using backend.Domain.Products;
using backend.Features.Common;
using backend.Features.Learning.Management;
using backend.Features.Products.Management;
using Microsoft.EntityFrameworkCore;
using NUlid;
using Xunit;

namespace Backend.Tests;

public sealed class ManagementScenarioTests
{
    private static readonly ModuleActor Actor = new(null, true);

    [Fact]
    public async Task CatalogCreationRecordsGeneratedIdentityInAudit()
    {
        await using var db = TestDatabase.Create();
        var result = await new CatalogService(db).Create(
            new CreateProductRequest("core", null, "Core", "Course", false, null), default, Actor);

        Assert.Null(result.Error);
        Assert.True(result.Value!.Id > 0);
        var audit = Assert.Single(await db.AuditLogs.ToListAsync());
        Assert.Equal(result.Value.Id.ToString(), audit.EntityId);
        Assert.Equal("create", audit.Action);
        // InMemory does not establish the relational transaction's rollback behavior.
    }

    [Fact]
    public async Task DuplicateCatalogCodeDoesNotCreateAnotherProductOrAudit()
    {
        await using var db = TestDatabase.Create();
        db.Products.Add(Product.Create("core", "Original", "Course", false));
        await db.SaveChangesAsync();
        var result = await new CatalogService(db).Create(
            new CreateProductRequest("core", null, "Duplicate", "Course", false, null), default, Actor);

        Assert.Equal(ModuleFailureKind.Conflict, result.Error!.Kind);
        Assert.Equal(1, await db.Products.CountAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task InvalidFilterStopsBeforeProviderSpecificQueries()
    {
        await using var db = TestDatabase.Create();
        var result = await new CatalogService(db).GetList(
            new ProductListQuery(0, 20, null, null, null, null, null, null), default, Actor);
        Assert.Equal(ModuleFailureKind.Validation, result.Error!.Kind);
        Assert.Contains("page", result.Error.Errors!.Keys);
    }

    [Fact]
    public async Task GroupCannotUsePackageFromAnotherCourse()
    {
        await using var db = TestDatabase.Create();
        var course = Product.Create("core", "Core", "Course", false);
        var other = Product.Create("other", "Other", "Course", false);
        db.AddRange(course, other);
        await db.SaveChangesAsync();
        var package = Package.Create("other-package", "Mentorship", other, 30);
        var cohort = Cohort.Create(course.Id, "October", DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
        db.AddRange(package, cohort);
        await db.SaveChangesAsync();

        var result = await new LearningAdministrationService(db).CreateGroup(
            new CreateGroupRequest(cohort.Id.ToString(), "A", null, package.Id, null), default, Actor);

        Assert.Equal("package_product_mismatch", result.Error!.Code);
        Assert.Empty(await db.Groups.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }
}
