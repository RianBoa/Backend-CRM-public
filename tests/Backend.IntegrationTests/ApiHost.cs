using System.Net.Http.Headers;
using backend.Auth;
using backend.Controllers;
using backend.Domain.Security;
using backend.Domain.Users;
using backend.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Backend.IntegrationTests;

internal sealed class ApiHost : IDisposable
{
    public static readonly JwtOptions Jwt = new()
    {
        Issuer = "ffa-integration", Audience = "ffa-integration",
        SigningKey = "integration-tests-only-not-a-runtime-secret-123456", AccessTokenMinutes = 15
    };
    private readonly TestServer server;
    public HttpClient Client { get; }

    public ApiHost(PostgresDatabase database, string? integrationKey = null, Action<IServiceCollection>? configure = null)
    {
        server = new TestServer(new WebHostBuilder().ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
            services.AddBackendSecurity(Jwt);
            if (integrationKey is not null)
                services.Configure<SalesIntegrationOptions>(SalesIntegrationOptions.Scheme, x => x.ApiKey = integrationKey);
            services.AddBackendFeatures();
            services.AddScoped(_ => database.CreateContext());
            services.AddSingleton(Options.Create(Jwt));
            services.AddSingleton<IUserPasswordService, UserPasswordService>();
            services.AddSingleton<IJwtTokenService, JwtTokenService>();
            services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
            configure?.Invoke(services);
        }).Configure(app =>
        {
            app.UseMiddleware<backend.Middleware.GlobalExceptionMiddleware>();
            app.UseRouting();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }));
        Client = server.CreateClient();
    }

    public async Task AuthenticateAsync(PostgresDatabase database, User actor)
    {
        await using var db = database.CreateContext();
        var version = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
            db.Users.Where(x => x.Id == actor.Id).Select(x => x.SecurityVersion));
        var session = RefreshToken.Create(actor.Id, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddDays(1), securityVersion: version);
        db.RefreshTokens.Add(session);
        await db.SaveChangesAsync();
        var token = new JwtTokenService(Options.Create(Jwt)).CreateAccessToken(actor, session.Id);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }

    public void Dispose() { Client.Dispose(); server.Dispose(); }
}
