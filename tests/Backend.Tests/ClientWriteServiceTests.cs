using backend.Domain.Clients;
using backend.Domain.Users;
using backend.Features.Clients.Commands;
using backend.Features.Clients.Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

// Source examples for the refactored command boundary; not executed as part of this refactor.
public sealed class ClientWriteServiceTests
{
    [Fact]
    public async Task EmailOnlyCreationUsesAuthenticatedActorAndRecordsAudit()
    {
        await using var db = TestDatabase.Create();
        var actor = AddActor(db);
        var request = new UpsertClientRequest("Student", [new("student@example.test", true)],
            null, null, null, "untrusted-body-value");

        var result = await new ClientWriteService(db).CreateAsync(request, actor, default);

        Assert.Null(result.Error);
        var client = await db.Clients.Include(x => x.Emails).SingleAsync();
        Assert.Equal("student@example.test", client.PrimaryEmail!.Value);
        Assert.Equal(actor.Id.ToString(), client.CreatedBy);
        Assert.Equal(actor.Id, (await db.AuditLogs.SingleAsync()).UserId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementCanSwitchBetweenEmailAndPhoneWithoutLosingContact(bool usePhone)
    {
        await using var db = TestDatabase.Create();
        var actor = AddActor(db);
        var client = usePhone
            ? Client.Create("Student", Email.From("old@example.test"))
            : Client.Create("Student", null, ContactInfo.Create(phoneNumber: "123456789"));
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        var request = new UpsertClientRequest("Updated",
            usePhone ? [] : [new("new@example.test", true)],
            usePhone ? new ClientContactsRequest("987654321", null, null, null) : null,
            null, null, null);

        var result = await new ClientWriteService(db).ReplaceAsync(client.Id, request, actor, default);

        Assert.Null(result.Error);
        Assert.Equal(usePhone ? "987654321" : null, client.Contacts.PhoneNumber);
        Assert.Equal(usePhone ? null : "new@example.test", client.PrimaryEmail?.Value);
    }

    [Fact]
    public async Task ReplacingOnlyEmailAddsTheNewOneBeforeRemovingTheOldOne()
    {
        await using var db = TestDatabase.Create();
        var actor = AddActor(db);
        var client = Client.Create("Student", Email.From("old@example.test"));
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        var request = new UpsertClientRequest("Student", [new("new@example.test", true)], null, null, null, null);

        var result = await new ClientWriteService(db).ReplaceAsync(client.Id, request, actor, default);

        Assert.Null(result.Error);
        Assert.Equal("new@example.test", Assert.Single(client.Emails).Email.Value);
    }

    [Fact]
    public async Task DuplicateEmailIsAConflictWithoutAnAuditWrite()
    {
        await using var db = TestDatabase.Create();
        var actor = AddActor(db);
        var client = Client.Create("Student", Email.From("student@example.test"));
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var result = await new ClientWriteService(db).AddEmailAsync(client.Id,
            new(" STUDENT@example.test ", false), actor, default);

        Assert.Equal("email_already_exists", result.Error!.Code);
        Assert.Single(client.Emails);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task FinancialPermissionIsCheckedInsideTheCommandBoundary()
    {
        await using var db = TestDatabase.Create();
        var actor = AddActor(db);
        var client = Client.Create("Student", Email.From("student@example.test"), financials: new Financials(100, 200));
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        var request = new PatchClientRequest("Must not change", null, new(999, 999), null);

        var result = await new ClientWriteService(db).PatchAsync(client.Id, request,
            actor with { CanManageFinancials = false }, default);

        Assert.Equal(ClientFailure.Forbidden, result.Error!.Kind);
        Assert.Equal("Student", client.Name);
        Assert.Equal(100m, client.Financials.InitialBudget);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    private static ClientActor AddActor(backend.Infrastructure.ApplicationDbContext db)
    {
        var user = User.Create("admin@example.test", "@admin", "test-only-hash", User.UserRole.Admin, "Admin");
        db.Users.Add(user);
        return new(user.Id, true);
    }
}
