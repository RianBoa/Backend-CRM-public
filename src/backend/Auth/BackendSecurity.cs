using System.Security.Claims;
using System.Text;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NUlid;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace backend.Auth;

public static class BackendSecurity
{
    public static IServiceCollection AddBackendSecurity(this IServiceCollection services, JwtOptions jwt)
    {
        services.AddSalesIntegrationSecurity();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                }));
            options.AddPolicy("sales-ingress", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                }));
            options.OnRejected = async (context, ct) =>
                await context.HttpContext.Response.WriteAsJsonAsync(new ErrorResponse("rate_limited", "Too many authentication attempts. Try again later."), ct);
        });
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new()
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true, ValidateLifetime = true,
                ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.FromSeconds(30)
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var principal = context.Principal;
                    if (!Ulid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
                        !Ulid.TryParse(principal?.FindFirstValue("sid"), out var sessionId))
                    {
                        context.Fail("Invalid user or session.");
                        return;
                    }
                    var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                    var now = DateTimeOffset.UtcNow;
                    var user = await db.RefreshTokens.AsNoTracking()
                        .Where(x => x.Id == sessionId && x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > now &&
                            x.SecurityVersion == x.User.SecurityVersion)
                        .Select(x => x.User).FirstOrDefaultAsync(context.HttpContext.RequestAborted);
                    if (user is null || !user.IsActive)
                    {
                        context.Fail("Inactive user or revoked session.");
                        return;
                    }
                    // Never trust stale role/permission claims after an administrator changes a user.
                    var identity = (ClaimsIdentity)principal!.Identity!;
                    foreach (var claim in identity.FindAll("permission").Concat(identity.FindAll(identity.RoleClaimType)).ToArray())
                        identity.RemoveClaim(claim);
                    identity.AddClaim(new(identity.RoleClaimType, user.Role.ToString()));
                    identity.AddClaims(AuthPolicies.GetPermissions(user.Role).Select(p => new Claim("permission", p)));
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new ErrorResponse("unauthorized", "Authentication is required."));
                },
                OnForbidden = async context =>
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new ErrorResponse("forbidden", "You do not have permission to access this resource."));
                }
            };
        });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            foreach (var permission in AuthPolicies.All)
                options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireClaim("permission", permission));
        });
        return services;
    }
}
