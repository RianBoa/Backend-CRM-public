using backend.Auth;
using backend.Domain.Applications;
using backend.Domain.RoadMaps;
using backend.Domain.Users;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using DomainUser = backend.Domain.Users.User;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Users.Management;

public sealed partial class TeamAdministrationService
{
    private IQueryable<UserDetailResponse> BuildDetailQuery(Ulid userId)
        => dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new UserDetailResponse(
                user.Id.ToString(),
                user.ShortId,
                user.FullName,
                user.Email,
                user.TelegramUsername,
                user.TelegramWork,
                user.Role.ToString(),
                user.IsActive,
                user.CreatedAt,
                dbContext.Applications.Count(application => application.AuthorId == user.Id),
                dbContext.Applications.Count(application =>
                    application.AuthorId == user.Id &&
                    application.Status != Application.ApplicationStatus.Closed &&
                    application.Status != Application.ApplicationStatus.Rejected),
                dbContext.RoadMapAssignments.Count(assignment => assignment.UserId == user.Id),
                dbContext.RoadMapAssignments.Count(assignment =>
                    assignment.UserId == user.Id &&
                    assignment.Status == RoadMapAssignment.AssignmentStatus.Pending)));

    private static IQueryable<DomainUser> ApplySorting(IQueryable<DomainUser> query, string sortBy, string sortDir)
    {
        var descending = string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy switch
        {
            "email" => descending ? query.OrderByDescending(user => user.Email).ThenByDescending(user => user.Id) : query.OrderBy(user => user.Email).ThenBy(user => user.Id),
            "role" => descending ? query.OrderByDescending(user => user.Role).ThenByDescending(user => user.Id) : query.OrderBy(user => user.Role).ThenBy(user => user.Id),
            "createdAt" => descending ? query.OrderByDescending(user => user.CreatedAt).ThenByDescending(user => user.Id) : query.OrderBy(user => user.CreatedAt).ThenBy(user => user.Id),
            _ => descending ? query.OrderByDescending(user => user.FullName).ThenByDescending(user => user.Id) : query.OrderBy(user => user.FullName).ThenBy(user => user.Id)
        };
    }

    private static string NormalizeSortField(string? sortBy)
        => string.IsNullOrWhiteSpace(sortBy) ? "fullName" : (AllowedSortFields.FirstOrDefault(field => string.Equals(field, sortBy.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "fullName");

    private static string NormalizeSortDirection(string? sortDir)
        => string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

    private static bool TryParseUlid(string? value, out Ulid ulid)
        => Ulid.TryParse(value?.Trim(), out ulid);

    private static Dictionary<string, string[]> ValidateCreateUserRequest(CreateUserRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = ["Email is required."];
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            errors["fullName"] = ["Full name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.TelegramUsername))
        {
            errors["telegramUsername"] = ["Telegram username is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            errors["role"] = ["Role is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = ["Password is required."];
        }
        else if (request.Password.Trim().Length < 8)
        {
            errors["password"] = ["Password must be at least 8 characters long."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidatePatchUserRequest(PatchUserRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (request.Email is not null && string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = ["Email cannot be empty."];
        }

        if (request.FullName is not null && string.IsNullOrWhiteSpace(request.FullName))
        {
            errors["fullName"] = ["Full name cannot be empty."];
        }

        if (request.TelegramUsername is not null && string.IsNullOrWhiteSpace(request.TelegramUsername))
        {
            errors["telegramUsername"] = ["Telegram username cannot be empty."];
        }

        if (request.Password is not null && request.Password.Trim().Length < 8)
        {
            errors["password"] = ["Password must be at least 8 characters long."];
        }

        return errors;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task RevokeSessionsAsync(Ulid userId, CancellationToken ct)
    {
        // A login already verifying the old password can still insert a session after this query
        // Its captured version will no longer match, even if the session wasn't seen here
        dbContext.Users.Local.Single(x => x.Id == userId).InvalidateSessions();
        var sessions = await dbContext.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var session in sessions) session.Revoke(DateTimeOffset.UtcNow);
    }

    private async Task SaveUserChangeAsync(string action, Ulid userId, Ulid? actorId, CancellationToken ct)
    {
        dbContext.AuditLogs.Add(backend.Domain.Auditing.AuditLog.Create(actorId, action, "user", userId.ToString()));
        await dbContext.SaveChangesAsync(ct);
    }
}
