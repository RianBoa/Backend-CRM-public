using backend.Domain.Applications;
using backend.Domain.Clients;
using backend.Domain.Users;
using backend.Features.Applications;
using backend.Features.Clients.Profile;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TeamRequest = backend.Domain.Applications.Application;

namespace Backend.Tests;

public sealed class ApplicationWorkTests
{
    [Fact]
    public async Task ReplyAndClosureSaveTogetherWithRealProcessor()
    {
        await using var db = TestDatabase.Create();
        var (actor, application) = await SeedAsync(db);
        var result = await Service(db).ReplyAsync(application.Id, actor.Id, "  Transfer completed  ",
            TeamRequest.ApplicationStatus.Closed, default);

        Assert.Null(result.Error);
        Assert.Equal("Transfer completed", result.Value!.Reply.Body);
        Assert.Equal(actor.Id.ToString(), result.Value.Reply.AuthorId);
        Assert.Equal(actor.Id, application.ProcessedById);
        Assert.NotNull(application.ProcessedAt);
        Assert.Equal(TeamRequest.ApplicationStatus.Closed, application.Status);
        Assert.Single(await db.ApplicationReplies.ToListAsync());
        Assert.Single(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ClosedApplicationRejectsReplyWithoutPartialWrite()
    {
        await using var db = TestDatabase.Create();
        var (actor, application) = await SeedAsync(db);
        application.ChangeStatus(TeamRequest.ApplicationStatus.Closed, actor.Id);
        await db.SaveChangesAsync();

        var result = await Service(db).ReplyAsync(application.Id, actor.Id, "Too late", null, default);

        Assert.Equal(WorkFailure.Conflict, result.Error!.Kind);
        Assert.Empty(await db.ApplicationReplies.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task DeactivatedActorCannotReply()
    {
        await using var db = TestDatabase.Create();
        var (actor, application) = await SeedAsync(db);
        actor.Deactivate();
        await db.SaveChangesAsync();

        var result = await Service(db).ReplyAsync(application.Id, actor.Id, "Reply", null, default);

        Assert.Equal(WorkFailure.Forbidden, result.Error!.Kind);
        Assert.Empty(await db.ApplicationReplies.ToListAsync());
    }

    [Fact]
    public async Task InvalidTransitionDoesNotLeaveAReply()
    {
        await using var db = TestDatabase.Create();
        var (actor, application) = await SeedAsync(db);
        application.ChangeStatus(TeamRequest.ApplicationStatus.InProcess, actor.Id);
        await db.SaveChangesAsync();
        var result = await Service(db).ReplyAsync(application.Id, actor.Id, "Reply",
            TeamRequest.ApplicationStatus.New, default);

        Assert.Equal(WorkFailure.Conflict, result.Error!.Kind);
        Assert.Empty(application.Replies);
        Assert.Empty(await db.ApplicationReplies.ToListAsync());
    }

    [Fact]
    public async Task RepliesAreScopedAndPaginated()
    {
        await using var db = TestDatabase.Create();
        var (actor, application) = await SeedAsync(db);
        var other = TeamRequest.Create(application.Type, actor.Id, "Other", TeamRequest.ApplicationReason.Other);
        db.Applications.Add(other);
        await db.SaveChangesAsync();
        var service = Service(db);
        await service.ReplyAsync(application.Id, actor.Id, "First", null, default);
        await service.ReplyAsync(application.Id, actor.Id, "Second", null, default);
        await service.ReplyAsync(other.Id, actor.Id, "Other conversation", null, default);

        var page = await service.GetRepliesAsync(application.Id, new(Page: 2, PageSize: 1), default);

        Assert.Equal(2, page!.Total);
        Assert.Equal("Second", Assert.Single(page.Items).Body);
    }

    [Fact]
    public async Task RepeatingStatusDoesNotDuplicateAuditOrChangeProcessedTime()
    {
        await using var db = TestDatabase.Create();
        var (actor, application) = await SeedAsync(db);
        var service = Service(db);
        await service.ChangeStatusAsync(application.Id, actor.Id, TeamRequest.ApplicationStatus.Closed, default);
        var processedAt = application.ProcessedAt;
        await service.ChangeStatusAsync(application.Id, actor.Id, TeamRequest.ApplicationStatus.Closed, default);

        Assert.Equal(processedAt, application.ProcessedAt);
        Assert.Single(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ClientProfileChangeIsLinkedToApplicationAndPreservesContacts()
    {
        await using var db = TestDatabase.Create();
        var client = Client.Create("Student", Email.From("student@example.test"));
        db.Clients.Add(client);
        var (actor, application) = await SeedAsync(db, client);

        var result = await Service(db).UpdateClientProfileAsync(application.Id, actor.Id,
            new ClientProfile("Updated", "Note", "uk", null, "Kyiv", null, null), default);

        Assert.Null(result.Error);
        Assert.Equal("Updated", client.Name);
        Assert.Single(client.Emails);
        var logs = await db.AuditLogs.ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, x => x.EntityType == "application" && x.EntityId == application.Id.ToString());
        Assert.Contains(logs, x => x.EntityType == "client" && x.MetadataJson!.Contains(application.Id.ToString()));
    }

    [Fact]
    public async Task ClosedApplicationCannotChangeClientProfile()
    {
        await using var db = TestDatabase.Create();
        var client = Client.Create("Student", Email.From("student@example.test"));
        db.Clients.Add(client);
        var (actor, application) = await SeedAsync(db, client);
        application.ChangeStatus(TeamRequest.ApplicationStatus.Closed, actor.Id);
        await db.SaveChangesAsync();

        var result = await Service(db).UpdateClientProfileAsync(application.Id, actor.Id,
            new ClientProfile("Wrong", null, null, null, null, null, null), default);

        Assert.Equal(WorkFailure.Conflict, result.Error!.Kind);
        Assert.Equal("Student", client.Name);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankReplyDoesNotChangeRevision(string body)
    {
        var application = TeamRequest.Create(ApplicationType.Create("help", "Help", User.UserRole.OnBoarding),
            NUlid.Ulid.NewUlid(), "Request", TeamRequest.ApplicationReason.Other);
        var revision = application.Revision;
        Assert.Throws<ArgumentException>(() => application.AddReply(NUlid.Ulid.NewUlid(), body, DateTime.UtcNow));
        Assert.Equal(revision, application.Revision);
        Assert.Empty(application.Replies);
    }

    private static ApplicationWorkService Service(ApplicationDbContext db)
        => new(db, new ClientProfileService(db), TimeProvider.System);

    private static async Task<(User Actor, TeamRequest Application)> SeedAsync(ApplicationDbContext db, Client? client = null)
    {
        var actor = User.Create("actor@example.test", "@actor", "test-only-hash", User.UserRole.OnBoarding, "Actor");
        var type = ApplicationType.Create("transfer", "Transfer", User.UserRole.OnBoarding);
        var application = TeamRequest.Create(type, actor.Id, "Transfer student", TeamRequest.ApplicationReason.Other, clientId: client?.Id);
        db.Users.Add(actor);
        db.Applications.Add(application);
        await db.SaveChangesAsync();
        return (actor, application);
    }
}
