using backend.Domain.Cases;
using backend.Domain.Clients;
using backend.Domain.LearningStructure;
using backend.Domain.Orders;
using backend.Domain.Subscriptions;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUlid;
using Xunit;

namespace Backend.Tests;

public sealed class RelationshipModelTests
{
    // Accessing model metadata does not open a connection or require a running database.
    private static ApplicationDbContext CreateModelContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=unused;Password=unused")
            .ConfigureWarnings(w => w.Throw(CoreEventId.ShadowForeignKeyPropertyCreated))
            .Options);

    public static IEnumerable<object[]> Relationships()
    {
        yield return [typeof(Case), typeof(Client), nameof(Case.ClientId), nameof(Case.Client),
            nameof(Client.Cases), DeleteBehavior.Cascade, typeof(Ulid), "ClientId"];
        yield return [typeof(AppliedBenefit), typeof(Order), nameof(AppliedBenefit.OrderId), nameof(AppliedBenefit.Order),
            nameof(Order.AppliedBenefits), DeleteBehavior.Cascade, typeof(Ulid), "OrderId"];
        yield return [typeof(AppliedBenefit), typeof(PromoBenefitRule), nameof(AppliedBenefit.PromoBenefitRuleId),
            nameof(AppliedBenefit.PromoBenefitRule), nameof(PromoBenefitRule.AppliedBenefits),
            DeleteBehavior.Restrict, typeof(int), "PromoBenefitRuleId"];
        yield return [typeof(DiscordAccess), typeof(Subscription), nameof(DiscordAccess.SubscriptionId),
            nameof(DiscordAccess.Subscription), nameof(Subscription.DiscordAccesses),
            DeleteBehavior.Cascade, typeof(Ulid), "subscription_id"];
        yield return [typeof(PlatformAccess), typeof(Subscription), nameof(PlatformAccess.SubscriptionId),
            nameof(PlatformAccess.Subscription), nameof(Subscription.PlatformAccesses),
            DeleteBehavior.Cascade, typeof(Ulid), "subscription_id"];
        yield return [typeof(SubscriptionAdjustment), typeof(Subscription), nameof(SubscriptionAdjustment.SubscriptionId),
            nameof(SubscriptionAdjustment.Subscription), nameof(Subscription.Adjustments),
            DeleteBehavior.Cascade, typeof(Ulid), "subscription_id"];
    }

    [Theory]
    [MemberData(nameof(Relationships))]
    public void CollectionAndReferenceUseOneExplicitForeignKey(Type dependent, Type principal,
        string key, string reference, string collection, DeleteBehavior deleteBehavior,
        Type keyType, string column)
    {
        using var db = CreateModelContext();
        var entity = db.Model.FindEntityType(dependent)!;
        var relationship = Assert.Single(entity.GetForeignKeys()
            .Where(x => x.PrincipalEntityType.ClrType == principal));
        var property = Assert.Single(relationship.Properties);

        Assert.Equal(key, property.Name);
        Assert.Equal(keyType, property.ClrType);
        Assert.Equal(column, property.GetColumnName());
        Assert.False(property.IsShadowProperty());
        Assert.True(relationship.IsRequired);
        Assert.False(relationship.IsUnique);
        Assert.Equal(reference, relationship.DependentToPrincipal?.Name);
        Assert.Equal(collection, relationship.PrincipalToDependent?.Name);
        Assert.Equal(deleteBehavior, relationship.DeleteBehavior);
    }

    [Fact]
    public void NonOwnedEntitiesHaveNoImplicitForeignKeyProperties()
    {
        using var db = CreateModelContext();
        var implicitKeys = db.Model.GetEntityTypes().Where(x => !x.IsOwned())
            .SelectMany(x => x.GetForeignKeys())
            .SelectMany(x => x.Properties)
            .Where(x => x.IsShadowProperty());

        Assert.Empty(implicitKeys);
    }

    [Fact]
    public void RemovingGroupKeepsEnrollmentHistory()
    {
        using var db = CreateModelContext();
        var enrollment = db.Model.FindEntityType(typeof(Enrollment))!;
        var relationship = Assert.Single(enrollment.GetForeignKeys()
            .Where(x => x.PrincipalEntityType.ClrType == typeof(Group)));

        Assert.Equal(nameof(Enrollment.GroupId), Assert.Single(relationship.Properties).Name);
        Assert.False(relationship.IsRequired);
        Assert.Equal(DeleteBehavior.SetNull, relationship.DeleteBehavior);
    }
}
