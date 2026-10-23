using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace backend.Auth;

public sealed class SalesIntegrationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "SalesIntegration";
    public const string Policy = "receive_sales_orders";
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class SalesIntegrationAuthentication(
    IOptionsMonitor<SalesIntegrationOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<SalesIntegrationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Options.ApiKey is not { Length: > 0 and <= 512 } expectedKey || Encoding.UTF8.GetByteCount(expectedKey) < 32)
            return Task.FromResult(AuthenticateResult.Fail("Sales integration is disabled."));
        if (!Request.Headers.TryGetValue("X-Integration-Key", out var supplied))
            return Task.FromResult(AuthenticateResult.NoResult());
        if (supplied.Count != 1 || supplied[0] is not { Length: > 0 and <= 512 } key)
            return Task.FromResult(AuthenticateResult.Fail("Invalid integration credentials."));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedKey));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash))
            return Task.FromResult(AuthenticateResult.Fail("Invalid integration credentials."));
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "sales-connector"), new Claim("integration", "sales")
        ], SalesIntegrationOptions.Scheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        return Response.WriteAsJsonAsync(new ErrorResponse("integration_unauthorized", "Valid integration credentials are required."));
    }
}

public static class SalesIntegrationSecurity
{
    public static IServiceCollection AddSalesIntegrationSecurity(this IServiceCollection services)
    {
        services.AddAuthentication().AddScheme<SalesIntegrationOptions, SalesIntegrationAuthentication>(
            SalesIntegrationOptions.Scheme, _ => { });
        services.AddOptions<SalesIntegrationOptions>(SalesIntegrationOptions.Scheme).BindConfiguration("SalesIntegration");
        services.AddAuthorization(options => options.AddPolicy(SalesIntegrationOptions.Policy, policy => policy
            .AddAuthenticationSchemes(SalesIntegrationOptions.Scheme).RequireAuthenticatedUser().RequireClaim("integration", "sales")));
        return services;
    }
}
