using System.Text.Json;
using backend.Domain.Clients;
using backend.Features.Clients.Contracts;
using backend.Features.Clients.Directory;
using backend.Features.Common;
using backend.Features.Subscriptions.Management;
using backend.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUlid;
using Xunit;

namespace Backend.Tests;

public sealed class ReviewRegressionTests
{
    [Fact]
    public async Task OrdinaryActorCannotCreateManualSubscriptionEvenThroughService()
    {
        await using var db = TestDatabase.Create();
        var result = await new SubscriptionManagementService(db).Create(
            new CreateSubscriptionRequest(Ulid.NewUlid().ToString(), 1, Ulid.NewUlid().ToString(),
                DateTime.UtcNow, DateTime.UtcNow.AddDays(1000), "CoursePlatform", "Active", null),
            default, new ModuleActor(Ulid.NewUlid(), false));

        Assert.Equal(ModuleFailureKind.Forbidden, result.Error!.Kind);
        Assert.Empty(await db.Subscriptions.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task OrdinaryActorCannotExtendManualSubscriptionDates()
    {
        await using var db = TestDatabase.Create();
        var result = await new SubscriptionManagementService(db).Patch(Ulid.NewUlid().ToString(),
            new PatchSubscriptionRequest(null, DateTime.UtcNow.AddDays(1000), null),
            default, new ModuleActor(Ulid.NewUlid(), false));
        Assert.Equal(ModuleFailureKind.Forbidden, result.Error!.Kind);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task MaximumCreatedToDoesNotOverflowAndStillIncludesClient()
    {
        await using var db = TestDatabase.Create();
        db.Clients.Add(Client.Create("Student", Email.From("student@example.test"),
            ContactInfo.Create("123456789"), new Financials(0, 0)));
        await db.SaveChangesAsync();
        var query = new ClientListQuery(1, 20, null, null, null, null, null, null, null, null, DateOnly.MaxValue);

        Assert.Empty(ClientDirectoryQueries.ValidateQuery(query));
        var page = await new ClientDirectoryQueries(db).GetListAsync(query, default);
        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task OrdinaryCreatedToRemainsInclusiveAndExcludesNextDay()
    {
        await using var db = TestDatabase.Create();
        var day = new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);
        db.Clients.Add(Client.Create("Included", Email.From("first@example.test"),
            ContactInfo.Create("123456789"), new Financials(0, 0), nowUtc: day.AddHours(23)));
        db.Clients.Add(Client.Create("Excluded", Email.From("second@example.test"),
            ContactInfo.Create("987654321"), new Financials(0, 0), nowUtc: day.AddDays(1)));
        await db.SaveChangesAsync();
        var query = new ClientListQuery(1, 20, null, null, null, null, null, null, null, null, new DateOnly(2026, 10, 23));
        var page = await new ClientDirectoryQueries(db).GetListAsync(query, default);
        Assert.Equal("Included", Assert.Single(page.Items).Name);
    }

    [Fact]
    public async Task InternalErrorUsesCamelCaseWithoutExceptionDetails()
    {
        using var services = new ServiceCollection().AddOptions().BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "review-test" };
        context.Response.Body = body;
        var middleware = new GlobalExceptionMiddleware(_ => throw new InvalidOperationException("secret SQL detail"),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.Invoke(context);
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Equal("internal_error", json.RootElement.GetProperty("code").GetString());
        Assert.False(json.RootElement.TryGetProperty("Code", out _));
        Assert.DoesNotContain("secret", json.RootElement.GetProperty("message").GetString()!);
    }
}
