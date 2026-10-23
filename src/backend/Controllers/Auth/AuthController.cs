using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using backend.Auth;
using backend.Domain.Security;
using backend.Domain.Users;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUlid;
using Microsoft.AspNetCore.RateLimiting;
using backend.Domain.Auditing;

namespace backend.Controllers;

[ApiController]
[Route("auth")]
public sealed partial class AuthController(
    ApplicationDbContext dbContext,
    IUserPasswordService passwordService,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ValidationError(ToErrorDictionary("body", "Request body is required."));
        }

        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = ["Email is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = ["Password is required."];
        }

        if (errors.Count > 0)
        {
            return ValidationError(errors);
        }

        var normalizedEmail = request.Email!.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive || !passwordService.VerifyPassword(user.PasswordHash, request.Password!))
        {
            return Unauthorized(new ErrorResponse("invalid_credentials", "Email or password is invalid."));
        }

        var refreshToken = refreshTokenService.CreateToken();

        var session = RefreshToken.Create(
            user.Id,
            refreshToken.TokenHash,
            refreshToken.ExpiresAtUtc,
            Request.Headers.UserAgent.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString(), user.SecurityVersion);
        dbContext.RefreshTokens.Add(session);
        var accessToken = jwtTokenService.CreateAccessToken(user, session.Id);

        dbContext.AuditLogs.Add(AuditLog.Create(user.Id, "login", "auth_session", session.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new LoginResponse(
            accessToken.AccessToken,
            refreshToken.PlainTextToken,
            "Bearer",
            accessToken.ExpiresAtUtc,
            accessToken.ExpiresInSeconds,
            BuildAuthenticatedUserResponse(user)));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponse>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ValidationError(ToErrorDictionary("refreshToken", "Refresh token is required."));
        }

        var incomingTokenHash = refreshTokenService.ComputeHash(request.RefreshToken.Trim());
        var existingToken = await dbContext.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == incomingTokenHash, cancellationToken);

        if (existingToken is null || !existingToken.IsActive || !existingToken.User.IsActive ||
            existingToken.SecurityVersion != existingToken.User.SecurityVersion)
        {
            return Unauthorized(new ErrorResponse("invalid_refresh_token", "Refresh token is invalid or expired."));
        }

        var newRefreshToken = refreshTokenService.CreateToken();

        existingToken.Revoke(DateTimeOffset.UtcNow, newRefreshToken.TokenHash);
        var session = RefreshToken.Create(
            existingToken.UserId,
            newRefreshToken.TokenHash,
            newRefreshToken.ExpiresAtUtc,
            Request.Headers.UserAgent.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString(), existingToken.SecurityVersion);
        dbContext.RefreshTokens.Add(session);
        var newAccessToken = jwtTokenService.CreateAccessToken(existingToken.User, session.Id);
        dbContext.AuditLogs.Add(AuditLog.Create(existingToken.UserId, "refresh", "auth_session", session.Id.ToString()));

        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            return Unauthorized(new ErrorResponse("invalid_refresh_token", "Refresh token was already used."));
        }

        return Ok(new LoginResponse(
            newAccessToken.AccessToken,
            newRefreshToken.PlainTextToken,
            "Bearer",
            newAccessToken.ExpiresAtUtc,
            newAccessToken.ExpiresInSeconds,
            BuildAuthenticatedUserResponse(existingToken.User)));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return NoContent();
        }

        var incomingTokenHash = refreshTokenService.ComputeHash(request.RefreshToken.Trim());
        var existingToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == incomingTokenHash, cancellationToken);

        if (existingToken is not null && existingToken.RevokedAtUtc is null)
        {
            existingToken.Revoke(DateTimeOffset.UtcNow);
            dbContext.AuditLogs.Add(AuditLog.Create(existingToken.UserId, "logout", "auth_session", existingToken.Id.ToString()));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Ulid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized(new ErrorResponse("invalid_token", "Authentication token is invalid."));
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == parsedUserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return Unauthorized(new ErrorResponse("user_not_found", "Authenticated user was not found or is inactive."));
        }

        return Ok(new MeResponse(BuildAuthenticatedUserResponse(user)));
    }

    private static AuthenticatedUserResponse BuildAuthenticatedUserResponse(User user)
        => new(
            user.Id.ToString(),
            user.ShortId,
            user.FullName,
            user.Email,
            user.TelegramUsername,
            user.TelegramWork,
            user.Role.ToString(),
            user.IsActive,
            AuthPolicies.GetPermissions(user.Role));
}
