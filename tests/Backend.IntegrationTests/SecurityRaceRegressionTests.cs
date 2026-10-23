using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.Auth;
using backend.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class SecurityRaceRegressionTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoginUsingOldCredentialsCannotSurviveEitherPasswordChangeRoute(bool patch)
    {
        var database = await postgres.CreateDatabaseAsync();
        var scenario = await Scenario.SeedAsync(database);
        using var gate = new PasswordGate();
        using var loginApi = new ApiHost(database, configure: s => s.AddSingleton<IUserPasswordService>(gate));
        using var adminApi = new ApiHost(database);
        await adminApi.AuthenticateAsync(database, scenario.Admin);
        // The login context has read the user and verified the old password before reset commits
        var pendingLogin = Task.Run(() => loginApi.Client.PostAsJsonAsync("/auth/login", new
            { email = scenario.Admin.Email, password = Scenario.Password }));
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var changed = patch
                ? await adminApi.Client.PatchAsJsonAsync($"/users/{scenario.Admin.Id}", new { password = "replacement-password" })
                : await adminApi.Client.PostAsJsonAsync($"/users/{scenario.Admin.Id}/password", new { password = "replacement-password" });
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        }
        finally { gate.Release(); }
        var response = await pendingLogin;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stale = (await response.Content.ReadFromJsonAsync<AuthController.LoginResponse>())!;
        loginApi.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stale.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await loginApi.Client.GetAsync("/auth/me")).StatusCode);
        loginApi.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await loginApi.Client.PostAsJsonAsync("/auth/refresh",
            new { refreshToken = stale.RefreshToken })).StatusCode);
        await using var read = database.CreateContext();
        var currentVersion = await read.Users.Select(x => x.SecurityVersion).SingleAsync();
        Assert.Equal(1L, currentVersion);
        Assert.Contains(await read.RefreshTokens.ToListAsync(), x => x.RevokedAtUtc == null && x.SecurityVersion != currentVersion);
    }
}
