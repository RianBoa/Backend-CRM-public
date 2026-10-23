using backend.Domain.Clients;
using backend.Domain.LearningStructure;
using backend.Domain.Orders;
using backend.Domain.Products;
using Xunit;
using Status = backend.Domain.LearningStructure.Enrollment.EnrollmentStatus;

namespace Backend.Tests;

public sealed class EnrollmentTests
{
    [Theory]
    [InlineData(Status.Graduated)]
    [InlineData(Status.AccessRevoked)]
    [InlineData(Status.Closed)]
    [InlineData(Status.Dropped)]
    public void TerminalEnrollmentCannotBeReactivated(Status terminal)
    {
        Assert.False(Enrollment.CanTransition(terminal, Status.Active));
    }

    [Fact]
    public void ActivationRequiresAssignment()
    {
        var enrollment = Enrollment.Create(Purchase());
        Assert.Throws<InvalidOperationException>(() => enrollment.ChangeStatus(Status.Active));
        Assert.Equal(Status.Pending, enrollment.Status);
    }

    [Fact]
    public void WrongProductDoesNotChangeAssignment()
    {
        var enrollment = Enrollment.Create(Purchase());
        Assert.Throws<InvalidOperationException>(() => enrollment.AssignToGroup(TargetGroup(productId: 2)));
        Assert.Null(enrollment.GroupId);
        Assert.Equal(Status.Pending, enrollment.Status);
    }

    [Fact]
    public void WrongPackageDoesNotChangeAssignment()
    {
        var enrollment = Enrollment.Create(Purchase());
        Assert.Throws<InvalidOperationException>(() => enrollment.AssignToGroup(TargetGroup(packageId: 99)));
        Assert.Null(enrollment.GroupId);
    }

    [Fact]
    public void ArchivedCohortCannotReceiveStudents()
    {
        var enrollment = Enrollment.Create(Purchase());
        var group = TargetGroup();
        group.Cohort.SetStatus(Cohort.CohortStatus.Archived);
        Assert.Throws<InvalidOperationException>(() => enrollment.AssignToGroup(group));
    }

    [Fact]
    public void TransferPreservesFrozenStatusAndUpdatesCohort()
    {
        var enrollment = Enrollment.Create(Purchase());
        enrollment.AssignToGroup(TargetGroup());
        enrollment.ChangeStatus(Status.Frozen);
        var target = TargetGroup();

        enrollment.AssignToGroup(target);

        Assert.Equal(target.Id, enrollment.GroupId);
        Assert.Equal(target.CohortId, enrollment.CohortId);
        Assert.Equal(Status.Frozen, enrollment.Status);
    }

    [Fact]
    public void RevocationCannotBeBypassedByGroupAssignment()
    {
        var enrollment = Enrollment.Create(Purchase());
        enrollment.AssignToGroup(TargetGroup());
        enrollment.ChangeStatus(Status.AccessRevoked);
        Assert.Throws<InvalidOperationException>(() => enrollment.AssignToGroup(TargetGroup()));
        Assert.Equal(Status.AccessRevoked, enrollment.Status);
    }

    private static OrderItem Purchase()
    {
        var product = Product.Create("course", "Course", "Course", false) with { Id = 1 };
        var package = Package.Create("standard", "Standard", product) with { Id = 10 };
        var client = Client.Create("Student", Email.From("student@example.test"));
        return OrderItem.Create(Order.Create(client.Id, OrderSource.Manual), product, package, 100, 1);
    }

    private static Group TargetGroup(int productId = 1, int? packageId = 10)
    {
        var cohort = Cohort.Create(productId, "Cohort", DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
        return Group.Create(cohort, "Group", packageId: packageId);
    }
}
