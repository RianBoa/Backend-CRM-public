using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using backend.Auth;
using backend.Features;
using backend.Contracts;
using backend.Controllers;
using backend.Domain.Clients;
using backend.Domain.Security;
using backend.Domain.Users;
using backend.Features.Clients.Financials;
using backend.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NUlid;
using Xunit;

namespace Backend.Tests;

public sealed class SecurityHttpTests
{
    [Fact]
    public async Task HumanAdminTokenDoesNotAuthorizeSalesIngress()
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.Admin);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/integrations/sales/orders", new { })).StatusCode);
    }

    [Fact]
    public async Task IntegrationCredentialsAreNarrowAndRejectUnknownFinancialFields()
    {
        const string key = "test-only-sales-key-not-a-runtime-secret-12345";
        using var host = await SecurityHost.StartAsync(key);
        host.Client.DefaultRequestHeaders.Add("X-Integration-Key", key);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/integrations/sales/orders", new { initialBudget = 100 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/clients")).StatusCode);
    }

    [Theory]
    [InlineData("GET", "/clients")]
    [InlineData("GET", "/clients/select")]
    [InlineData("GET", "/clients/01K00000000000000000000001")]
    [InlineData("GET", "/clients/01K00000000000000000000001/financials")]
    [InlineData("POST", "/users")]
    public async Task PrivateEndpointsRejectAnonymousRequests(string method, string path)
    {
        using var host = await SecurityHost.StartAsync();
        var response = await host.Client.SendAsync(new(new HttpMethod(method), path));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(User.UserRole.Admin, true)]
    [InlineData(User.UserRole.TeamLead, true)]
    [InlineData(User.UserRole.ProjectManager, true)]
    [InlineData(User.UserRole.OnBoarding, false)]
    [InlineData(User.UserRole.Curator, false)]
    [InlineData(User.UserRole.TroubleShooter, false)]
    [InlineData(User.UserRole.Developer, false)]
    public async Task FinancialReadRequiresAnExplicitPermission(User.UserRole role, bool allowed)
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(role);
        var response = await host.Client.GetAsync($"/clients/{host.ClientId}/financials");
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        if (allowed) Assert.Equal(100m, (await response.Content.ReadFromJsonAsync<ClientFinancialsBlock>())!.InitialBudget);
    }

    [Fact]
    public async Task TeamLeadCanReadButCannotWriteFinancials()
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.TeamLead);
        var response = await host.Client.PutAsJsonAsync($"/clients/{host.ClientId}/financials", new { initialBudget = 900, totalEarnings = 900 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = host.Database();
        Assert.Equal(100m, (await db.Clients.SingleAsync()).Financials.InitialBudget);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task AdminFinancialWriteIsAudited()
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.Admin);
        var response = await host.Client.PutAsJsonAsync($"/clients/{host.ClientId}/financials", new { initialBudget = 900, totalEarnings = 200 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = host.Database();
        Assert.Equal(900m, (await db.Clients.SingleAsync()).Financials.InitialBudget);
        Assert.Equal("replace_financials", (await db.AuditLogs.SingleAsync()).Action);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"initialBudget\":null,\"totalEarnings\":0}")]
    [InlineData("{\"initialBudget\":-1,\"totalEarnings\":0}")]
    public async Task InvalidFinancialBlockReturns400WithoutChanges(string json)
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.Admin);
        var response = await host.Client.PutAsync($"/clients/{host.ClientId}/financials", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = host.Database();
        Assert.Equal(100m, (await db.Clients.SingleAsync()).Financials.InitialBudget);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task MissingClientReturns404ToAuthorizedReader()
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.Admin);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/clients/{Ulid.NewUlid()}/financials")).StatusCode);
    }

    [Fact]
    public async Task ProfileBlockRejectsBlankNameAndAcceptsValidForm()
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.OnBoarding);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"/clients/{host.ClientId}/profile", new { name = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PutAsJsonAsync($"/clients/{host.ClientId}/profile", new { name = "Changed" })).StatusCode);
    }

    [Fact]
    public async Task PurchasePagingIsValidatedByHttpBinding()
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.OnBoarding);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync($"/clients/{host.ClientId}/purchases?pageSize=1000")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync($"/clients/{host.ClientId}/purchases")).StatusCode);
    }

    [Theory]
    [InlineData("price", "asc")]
    [InlineData("purchaseDate", "invalid")]
    public async Task UnsupportedPurchaseSortIsRejectedByHttp(string field, string direction)
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.OnBoarding);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync(
            $"/clients/{host.ClientId}/purchases?sortBy={field}&sortDir={direction}")).StatusCode);
    }

    [Fact]
    public async Task LoginAttemptsAreRateLimited()
    {
        using var host = await SecurityHost.StartAsync();
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/auth/login", new { email = "missing@example.test", password = "incorrect" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await host.Client.PostAsJsonAsync("/auth/login", new { email = "missing@example.test", password = "incorrect" })).StatusCode);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("POST")]
    public async Task LegacyClientWritesCannotBypassFinancialPermission(string method)
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.OnBoarding);
        var path = method == "POST" ? "/clients" : $"/clients/{host.ClientId}";
        var response = await host.Client.SendAsync(new(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { financials = new { initialBudget = 999, totalEarnings = 999 } })
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = host.Database();
        Assert.Equal(100m, (await db.Clients.SingleAsync()).Financials.InitialBudget);
    }

    [Fact]
    public async Task GeneralClientDetailDoesNotExposeFinancialBlock()
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.OnBoarding);
        var response = await host.Client.GetAsync($"/clients/{host.ClientId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.TryGetProperty("financials", out _));
        Assert.True(body.RootElement.TryGetProperty("products", out _));
    }

    [Fact]
    public async Task CaseAmountsAreHiddenInAllReadPathsWithoutFinancePermission()
    {
        using var host = await SecurityHost.StartAsync();
        var actor = await host.AuthenticateAsync(User.UserRole.Curator);
        var achievement = backend.Domain.Cases.Case.Create(host.ClientId, actor.Id,
            backend.Domain.Cases.Case.CaseType.Other, 1234m);
        await using (var db = host.Database())
        {
            db.Cases.Add(achievement);
            await db.SaveChangesAsync();
        }
        var page = await host.Client.GetFromJsonAsync<PagedResponse<backend.Features.Cases.Management.CaseListItemResponse>>("/cases");
        Assert.Null(Assert.Single(page!.Items).Amount);
        Assert.Null((await host.Client.GetFromJsonAsync<backend.Features.Cases.Management.CaseDetailResponse>($"/cases/{achievement.Id}"))!.Amount);
        using var detail = JsonDocument.Parse(await host.Client.GetStringAsync($"/clients/{host.ClientId}"));
        Assert.Equal(JsonValueKind.Null, detail.RootElement.GetProperty("cases")[0].GetProperty("amount").ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/cases?sortBy=amount")).StatusCode);
        await host.AuthenticateAsync(User.UserRole.Admin);
        Assert.Equal(1234m, (await host.Client.GetFromJsonAsync<backend.Features.Cases.Management.CaseDetailResponse>($"/cases/{achievement.Id}"))!.Amount);
    }

    [Theory]
    [InlineData(User.UserRole.TeamLead)]
    [InlineData(User.UserRole.ProjectManager)]
    public async Task UserReadersCannotPromoteThemselvesOrCreateAdmins(User.UserRole role)
    {
        using var host = await SecurityHost.StartAsync();
        var actor = await host.AuthenticateAsync(role);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PatchAsync($"/users/{actor.Id}", JsonContent.Create(new { role = "Admin" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PostAsJsonAsync("/users", new { role = "Admin" })).StatusCode);
    }

    [Fact]
    public async Task LegacyProfileReplacementWithoutFinancialsPreservesAmounts()
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.OnBoarding);
        var response = await host.Client.PutAsJsonAsync($"/clients/{host.ClientId}", new
        {
            name = "Updated", emails = new[] { new { email = "student@example.test", isPrimary = true } }
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = host.Database();
        Assert.Equal(100m, (await db.Clients.SingleAsync()).Financials.InitialBudget);
    }

    [Theory]
    [InlineData("GET", "/orders")]
    [InlineData("POST", "/orders")]
    [InlineData("POST", "/orders/01K00000000000000000000001/payments")]
    [InlineData("POST", "/users")]
    [InlineData("POST", "/cases")]
    [InlineData("POST", "/learning-structure/cohorts")]
    public async Task CuratorCannotUsePrivilegedLegacyEndpoints(string method, string path)
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.Curator);
        var response = await host.Client.SendAsync(new(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeactivationRejectsAnAlreadyIssuedAccessToken()
    {
        using var host = await SecurityHost.StartAsync();
        var actor = await host.AuthenticateAsync(User.UserRole.OnBoarding);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/auth/me")).StatusCode);
        await using (var db = host.Database())
        {
            (await db.Users.SingleAsync(x => x.Id == actor.Id)).Deactivate();
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/auth/me")).StatusCode);
    }

    [Fact]
    public async Task StaleAdminClaimsDoNotGrantPrivilegesAfterRoleChange()
    {
        using var host = await SecurityHost.StartAsync();
        var actor = await host.AuthenticateAsync(User.UserRole.Admin);
        await using (var db = host.Database())
        {
            var user = await db.Users.SingleAsync(x => x.Id == actor.Id);
            db.Entry(user).Property(nameof(User.Role)).CurrentValue = User.UserRole.Curator;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync($"/clients/{host.ClientId}/financials")).StatusCode);
    }

    [Fact]
    public async Task LogoutRevokesBothRefreshAndAccess()
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.OnBoarding);
        host.Client.DefaultRequestHeaders.Authorization = null;
        var response = await host.Client.PostAsJsonAsync("/auth/login", new { email = "onboarding@example.test", password = SecurityHost.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = (await response.Content.ReadFromJsonAsync<AuthController.LoginResponse>())!;
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PostAsJsonAsync("/auth/logout", new { refreshToken = login.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/auth/refresh", new { refreshToken = login.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task RefreshRotatesSessionAndRejectsReuse()
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.OnBoarding);
        var response = await host.Client.PostAsJsonAsync("/auth/login", new { email = "onboarding@example.test", password = SecurityHost.Password });
        var login = (await response.Content.ReadFromJsonAsync<AuthController.LoginResponse>())!;
        var refreshed = await host.Client.PostAsJsonAsync("/auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var next = (await refreshed.Content.ReadFromJsonAsync<AuthController.LoginResponse>())!;
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/auth/me")).StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", next.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/auth/refresh", new { refreshToken = login.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task PasswordResetRevokesExistingSessions()
    {
        using var host = await SecurityHost.StartAsync();
        var target = await host.AuthenticateAsync(User.UserRole.OnBoarding);
        var oldToken = host.Client.DefaultRequestHeaders.Authorization;
        await host.AuthenticateAsync(User.UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsJsonAsync($"/users/{target.Id}/password", new { password = "changed-test-password" })).StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = oldToken;
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/auth/me")).StatusCode);
    }

    [Fact]
    public async Task DeactivationThroughApiRevokesRefreshEvenAfterReactivation()
    {
        using var host = await SecurityHost.StartAsync();
        var target = await host.AuthenticateAsync(User.UserRole.OnBoarding);
        var oldToken = host.Client.DefaultRequestHeaders.Authorization;
        await host.AuthenticateAsync(User.UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsJsonAsync($"/users/{target.Id}/status", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsJsonAsync($"/users/{target.Id}/status", new { isActive = true })).StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = oldToken;
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/auth/me")).StatusCode);
        await using var db = host.Database();
        Assert.All(await db.RefreshTokens.Where(x => x.UserId == target.Id).ToListAsync(), x => Assert.NotNull(x.RevokedAtUtc));
    }

    [Fact]
    public async Task RoleChangeThroughApiRevokesExistingSessions()
    {
        using var host = await SecurityHost.StartAsync();
        var target = await host.AuthenticateAsync(User.UserRole.OnBoarding);
        var oldToken = host.Client.DefaultRequestHeaders.Authorization;
        await host.AuthenticateAsync(User.UserRole.Admin);
        var response = await host.Client.PatchAsync($"/users/{target.Id}", JsonContent.Create(new { role = "Curator" }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = oldToken;
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/auth/me")).StatusCode);
    }

    [Fact]
    public async Task AdminCannotRevokeOwnAdministrativeAccess()
    {
        using var host = await SecurityHost.StartAsync();
        var actor = await host.AuthenticateAsync(User.UserRole.Admin);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PatchAsync($"/users/{actor.Id}", JsonContent.Create(new { role = "Curator" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PostAsJsonAsync($"/users/{actor.Id}/status", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExpiredOrIncorrectlySignedJwtIsRejected(bool expired)
    {
        using var host = await SecurityHost.StartAsync();
        var token = new JwtSecurityToken(SecurityHost.Jwt.Issuer, SecurityHost.Jwt.Audience,
            notBefore: DateTime.UtcNow.AddHours(-2), expires: expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                expired ? SecurityHost.Jwt.SigningKey : "different-test-key-at-least-32-bytes-long")), SecurityAlgorithms.HmacSha256));
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("/products?page=0")]
    [InlineData("/products?sortBy=unsupported")]
    [InlineData("/users?role=SuperOwner")]
    [InlineData("/cases?clientId=123")]
    public async Task ResourceListRejectsInvalidFiltersThroughHttp(string route)
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.Admin);
        var response = await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task RecordParameterValidationRejectsBlankTextAndUnknownWriteFields()
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.Admin);
        var blank = await host.Client.PostAsJsonAsync("/products",
            new { code = "course", name = "  ", type = "Course", discordAccess = false });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        var unknown = await host.Client.PostAsJsonAsync("/products",
            new { code = "course", name = "Course", type = "Course", discordAccess = false, secretFlag = true });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        await using var db = host.Database();
        Assert.Empty(await db.Products.ToListAsync());
    }

    [Theory]
    [InlineData(User.UserRole.Curator)]
    [InlineData(User.UserRole.TroubleShooter)]
    [InlineData(User.UserRole.OnBoarding)]
    public async Task NonAdminCannotCreateManualActiveSubscription(User.UserRole role)
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(role);
        var response = await host.Client.PostAsJsonAsync("/subscriptions", new
        {
            clientId = host.ClientId.ToString(), productId = 1, orderItemId = Ulid.NewUlid().ToString(),
            startAt = DateTime.UtcNow, endAt = DateTime.UtcNow.AddYears(5),
            type = "CoursePlatform", status = "Active"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = host.Database();
        Assert.Empty(await db.Subscriptions.ToListAsync());
    }

    [Fact]
    public async Task AdminStillReachesManualOverrideValidation()
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(User.UserRole.Admin);
        var response = await host.Client.PostAsJsonAsync("/subscriptions", new
        {
            clientId = host.ClientId.ToString(), productId = 999, orderItemId = Ulid.NewUlid().ToString(),
            startAt = DateTime.UtcNow, endAt = DateTime.UtcNow.AddDays(1),
            type = "CoursePlatform", status = "Active"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("product_not_found", json.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(User.UserRole.Admin, HttpStatusCode.NotFound)]
    [InlineData(User.UserRole.OnBoarding, HttpStatusCode.NotFound)]
    [InlineData(User.UserRole.Curator, HttpStatusCode.Forbidden)]
    [InlineData(User.UserRole.TroubleShooter, HttpStatusCode.Forbidden)]
    public async Task ResumeAccessRequiresGrantManagementRole(User.UserRole role, HttpStatusCode expected)
    {
        using var host = await SecurityHost.StartAsync();
        await host.AuthenticateAsync(role);
        var response = await host.Client.PostAsync($"/subscriptions/{Ulid.NewUlid()}/resume-access", null);
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticationErrorsUseSameCamelCaseAsMvcErrors()
    {
        using var host = await SecurityHost.StartAsync();
        var unauthorized = await host.Client.GetAsync("/clients");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        using var first = JsonDocument.Parse(await unauthorized.Content.ReadAsStringAsync());
        Assert.Equal("unauthorized", first.RootElement.GetProperty("code").GetString());
        Assert.True(first.RootElement.TryGetProperty("message", out _));
        Assert.False(first.RootElement.TryGetProperty("Code", out _));

        await host.AuthenticateAsync(User.UserRole.Curator);
        var forbidden = await host.Client.GetAsync("/users");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var second = JsonDocument.Parse(await forbidden.Content.ReadAsStringAsync());
        Assert.Equal("forbidden", second.RootElement.GetProperty("code").GetString());
        Assert.True(second.RootElement.TryGetProperty("message", out _));
    }

    private sealed class SecurityHost(TestServer server, string databaseName) : IDisposable
    {
        public const string Password = "local-test-password";
        public static readonly JwtOptions Jwt = new() { Issuer = "test", Audience = "test", SigningKey = "test-only-signing-key-never-use-in-runtime-12345", AccessTokenMinutes = 15 };
        public HttpClient Client { get; } = server.CreateClient();
        public Ulid ClientId { get; private set; }
        public ApplicationDbContext Database() => TestDatabase.Create(databaseName);

        public static async Task<SecurityHost> StartAsync(string? integrationKey = null)
        {
            var name = Guid.NewGuid().ToString();
            var server = new TestServer(new WebHostBuilder().ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
                services.AddBackendSecurity(Jwt);
                if (integrationKey is not null)
                    services.Configure<SalesIntegrationOptions>(SalesIntegrationOptions.Scheme, x => x.ApiKey = integrationKey);
                services.AddScoped(_ => TestDatabase.Create(name));
                services.AddSingleton(Options.Create(Jwt));
                services.AddSingleton<IUserPasswordService, UserPasswordService>();
                services.AddSingleton<IJwtTokenService, JwtTokenService>();
                services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
                services.AddBackendFeatures();
            }).Configure(app =>
            {
                app.UseMiddleware<backend.Middleware.GlobalExceptionMiddleware>();
                app.UseRouting();
                app.UseRateLimiter();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }));
            var host = new SecurityHost(server, name);
            await using var db = host.Database();
            var client = backend.Domain.Clients.Client.Create("Student", Email.From("student@example.test"),
                ContactInfo.Create("123456789", null, null, null), new Financials(100, 200));
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            host.ClientId = client.Id;
            return host;
        }

        public async Task<User> AuthenticateAsync(User.UserRole role)
        {
            await using var db = Database();
            var user = User.Create($"{role.ToString().ToLowerInvariant()}@example.test", $"@{role}",
                new UserPasswordService().HashPassword(Password), role, role.ToString());
            var session = RefreshToken.Create(user.Id, Guid.NewGuid().ToString(), DateTimeOffset.UtcNow.AddDays(1));
            db.AddRange(user, session);
            await db.SaveChangesAsync();
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtTokenService(Options.Create(Jwt)).CreateAccessToken(user, session.Id).AccessToken);
            return user;
        }

        public void Dispose() { Client.Dispose(); server.Dispose(); }
    }
}
