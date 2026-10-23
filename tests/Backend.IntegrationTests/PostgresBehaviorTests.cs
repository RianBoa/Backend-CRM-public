using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Auth;
using backend.Controllers;
using backend.Domain.Auditing;
using backend.Domain.Cases;
using backend.Domain.LearningStructure;
using backend.Domain.Orders;
using backend.Domain.RoadMaps;
using backend.Domain.Security;
using backend.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Backend.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class PostgresBehaviorTests(PostgresFixture postgres)
{
    [Fact]
    public async Task MigrationsBuildAnEmptyDatabaseAndAreRepeatable()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateContext();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Contains("20260519000000_AddClientEmails", await db.Database.GetAppliedMigrationsAsync());
        await db.Database.MigrateAsync();
        Assert.Equal(0, await db.Clients.CountAsync());
        Assert.Equal(0, await db.RefreshTokens.CountAsync());
        Assert.Equal(0, await db.ApplicationReplies.CountAsync());
    }

    [Fact]
    public async Task PopulatedClientDetailAndRoadmapExecuteRealSql()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        await using (var db = database.CreateContext())
        {
            var subscription = Subscription.Create(scenario.Student.Id, scenario.Product, scenario.Purchase,
                DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(29), Subscription.SubscriptionType.CoursePlatform,
                Subscription.SubscriptionStatus.Active);
            var cohort = Cohort.Create(scenario.Product.Id, "October cohort", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(28));
            var group = Group.Create(cohort, "Group A", scenario.Admin.Id, scenario.Package.Id);
            var enrollment = Enrollment.Create(scenario.Purchase);
            enrollment.AssignToGroup(group);
            var template = RoadMapTemplate.Create("Achievements", scenario.Product.Id);
            // These are detached setup entities from the preceding context, not new rows.
            db.AttachRange(scenario.Product, scenario.Package, scenario.Order, scenario.Purchase);
            db.AddRange(subscription, cohort, group, enrollment, template);
            db.Cases.Add(Case.Create(scenario.Student.Id, scenario.Admin.Id, Case.CaseType.Other, 150m));
            await db.SaveChangesAsync();
            db.AddRange(DiscordAccess.Create(subscription.Id, "discord-user", "student-role", "Student", DateTime.UtcNow),
                PlatformAccess.Create(subscription.Id, "Learning", "platform-user", DateTime.UtcNow));
            var item = RoadMapItem.Create(template.Id, "First result", RoadMapItem.RoadMapItemType.Case, 7);
            db.Add(item);
            await db.SaveChangesAsync();
            db.Add(RoadMapAssignment.Create(item.Id, item, enrollment.Id, enrollment, 7, scenario.Admin.Id, scenario.Admin));
            db.Entry(scenario.Admin).State = EntityState.Unchanged;
            await db.SaveChangesAsync();
        }
        using var api = new ApiHost(database);
        await api.AuthenticateAsync(database, scenario.Admin);
        var response = await api.Client.GetAsync($"/clients/{scenario.Student.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var detail = json.RootElement;
        Assert.Equal(scenario.Student.Id.ToString(), detail.GetProperty("id").GetString());
        Assert.False(detail.TryGetProperty("financials", out _));
        Assert.Single(detail.GetProperty("products").EnumerateArray());
        Assert.Single(detail.GetProperty("discordAccesses").EnumerateArray());
        Assert.Single(detail.GetProperty("cases").EnumerateArray());
        Assert.NotEmpty(detail.GetProperty("productTimeline").EnumerateArray());
        await using var read = database.CreateContext();
        var enrollmentId = await read.Enrollments.Select(x => x.Id).SingleAsync();
        var checklist = await api.Client.GetAsync($"/learning/enrollments/{enrollmentId}/roadmap?sortBy=dueAt");
        Assert.Equal(HttpStatusCode.OK, checklist.StatusCode);
        using var rows = JsonDocument.Parse(await checklist.Content.ReadAsStringAsync());
        Assert.Equal(1, rows.RootElement.GetProperty("total").GetInt32());
        var directory = await api.Client.GetAsync("/learning/groups?search=Group");
        Assert.Equal(HttpStatusCode.OK, directory.StatusCode);
    }

    [Fact]
    public async Task ClientSearchAndPurchasePagingUsePostgresTranslation()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        using var api = new ApiHost(database);
        await api.AuthenticateAsync(database, scenario.Admin);
        var list = await api.Client.GetAsync("/clients?search=STUDENT&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("total").GetInt32());
        var purchases = await api.Client.GetAsync($"/clients/{scenario.Student.Id}/purchases?search=FFA&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, purchases.StatusCode);
        using var rows = JsonDocument.Parse(await purchases.Content.ReadAsStringAsync());
        Assert.Equal(1, rows.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(scenario.Purchase.Id.ToString(), rows.RootElement.GetProperty("items")[0].GetProperty("orderItemId").GetString());
    }

    [Fact]
    public async Task PaidPaymentsProvisionOnceAndGrantStartsTheTerm()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        using var api = new ApiHost(database);
        await api.AuthenticateAsync(database, scenario.Admin);
        var payment = new { amount = 25m, currency = "USD", status = "Paid", totalInstallments = 1 };
        var path = $"/orders/{scenario.Order.Id}/payments";
        Assert.Equal(HttpStatusCode.OK, (await api.Client.PostAsJsonAsync(path, payment)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.PostAsJsonAsync(path, payment)).StatusCode);
        await using var db = database.CreateContext();
        var subscriptions = await db.Subscriptions.ToListAsync();
        Assert.Equal(2, subscriptions.Count);
        Assert.All(subscriptions, x => { Assert.Equal(Subscription.SubscriptionStatus.AwaitingAccess, x.Status); Assert.Null(x.StartAt); Assert.Null(x.EndAt); });
        var target = subscriptions.Single(x => x.Type == Subscription.SubscriptionType.CoursePlatform);
        var grant = DateTimeOffset.UtcNow.AddMinutes(-1);
        var granted = await api.Client.PostAsJsonAsync($"/subscriptions/{target.Id}/activate-access", new { grantedAt = grant });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        await db.Entry(target).ReloadAsync();
        Assert.Equal(Subscription.SubscriptionStatus.Active, target.Status);
        Assert.Equal(30, (target.EndAt!.Value - target.StartAt!.Value).TotalDays);
        Assert.InRange(Math.Abs((target.StartAt.Value - grant.UtcDateTime).TotalMilliseconds), 0, 1);
        Assert.Equal(2, await db.Payments.CountAsync());
    }

    [Fact]
    public async Task UniqueProvisioningKeyRollsBackTheWholeSave()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        await using (var first = database.CreateContext())
        {
            first.AttachRange(scenario.Product, scenario.Package, scenario.Order, scenario.Purchase);
            first.Subscriptions.Add(Subscription.AwaitAccess(scenario.Purchase, Subscription.SubscriptionType.CoursePlatform, 30));
            await first.SaveChangesAsync();
        }
        await using (var second = database.CreateContext())
        {
            var purchase = await second.OrderItems.Include(x => x.Order).Include(x => x.Product).Include(x => x.Package).SingleAsync();
            second.Subscriptions.Add(Subscription.AwaitAccess(purchase, Subscription.SubscriptionType.CoursePlatform, 30));
            second.Payments.Add(Payment.Create(scenario.Order.Id, 25, "USD", "Paid"));
            second.AuditLogs.Add(AuditLog.Create(scenario.Admin.Id, "should_rollback", "order", scenario.Order.Id.ToString()));
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
            Assert.Equal("ux_subscription_provisioning_key", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
        }
        await using var read = database.CreateContext();
        Assert.Single(await read.Subscriptions.ToListAsync());
        Assert.Empty(await read.Payments.ToListAsync());
        Assert.Empty(await read.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task RefreshConcurrencyAllowsOnlyOneReplacementAndAudit()
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        var original = RefreshToken.Create(scenario.Admin.Id, "original-token-hash", DateTimeOffset.UtcNow.AddDays(1));
        await using (var seed = database.CreateContext()) { seed.RefreshTokens.Add(original); await seed.SaveChangesAsync(); }
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var left = await first.RefreshTokens.SingleAsync();
        var right = await second.RefreshTokens.SingleAsync();
        var now = DateTimeOffset.UtcNow;
        left.Revoke(now, "left-hash");
        right.Revoke(now, "right-hash");
        first.AddRange(RefreshToken.Create(scenario.Admin.Id, "left-hash", now.AddDays(1)), AuditLog.Create(scenario.Admin.Id, "refresh", "auth_session"));
        second.AddRange(RefreshToken.Create(scenario.Admin.Id, "right-hash", now.AddDays(1)), AuditLog.Create(scenario.Admin.Id, "refresh", "auth_session"));
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var read = database.CreateContext();
        Assert.Equal(2, await read.RefreshTokens.CountAsync());
        Assert.Equal(1, await read.RefreshTokens.CountAsync(x => x.RevokedAtUtc == null));
        Assert.Single(await read.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task JwtRefreshLogoutAndFinancialAuthorizationUseTheDatabase()
    {
        var database = await postgres.CreateDatabaseAsync();
        await Scenario.SeedAsync(database);
        using var api = new ApiHost(database);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.GetAsync("/clients")).StatusCode);
        var loginResponse = await api.Client.PostAsJsonAsync("/auth/login", new { email = "admin@example.test", password = Scenario.Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = (await loginResponse.Content.ReadFromJsonAsync<AuthController.LoginResponse>())!;
        api.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/auth/me")).StatusCode);
        var refresh = await api.Client.PostAsJsonAsync("/auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var next = (await refresh.Content.ReadFromJsonAsync<AuthController.LoginResponse>())!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.GetAsync("/auth/me")).StatusCode);
        api.Client.DefaultRequestHeaders.Authorization = new("Bearer", next.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await api.Client.PostAsJsonAsync("/auth/logout", new { refreshToken = next.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.GetAsync("/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.PostAsJsonAsync("/auth/refresh", new { refreshToken = next.RefreshToken })).StatusCode);
    }
}
