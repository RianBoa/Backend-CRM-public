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
    public async Task<ModuleOutcome<UserDetailResponse>> Create(CreateUserRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateCreateUserRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        if (!Enum.TryParse<DomainUser.UserRole>(request.Role, true, out var role) || !Enum.IsDefined(role))
        {
            return Invalid(ToErrorDictionary("role", "Role is invalid."));
        }

        var normalizedEmail = request.Email!.Trim().ToLowerInvariant();
        var normalizedTelegramUsername = request.TelegramUsername!.Trim();
        var normalizedTelegramWork = NormalizeOptional(request.TelegramWork);

        if (await dbContext.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
        {
            return Conflict(new ErrorResponse("user_email_conflict", "Email already exists."));
        }

        if (await dbContext.Users.AnyAsync(user => user.TelegramUsername == normalizedTelegramUsername, cancellationToken))
        {
            return Conflict(new ErrorResponse("user_telegram_conflict", "Telegram username already exists."));
        }

        var passwordHash = passwordService.HashPassword(request.Password!);

        try
        {
            var user = DomainUser.Create(
                normalizedEmail,
                normalizedTelegramUsername,
                passwordHash,
                role,
                request.FullName!.Trim(),
                normalizedTelegramWork);

            if (request.IsActive is false)
            {
                user.Deactivate();
            }

            dbContext.Users.Add(user);
            await SaveUserChangeAsync("create", user.Id, actor.Id, cancellationToken);

            var response = await BuildDetailQuery(user.Id).FirstAsync(cancellationToken);
            return ModuleOutcome<UserDetailResponse>.Success(response);
        }
        catch (ArgumentException ex)
        {
            return Invalid(ToErrorDictionary("request", ex.Message));
        }
    }

    public async Task<ModuleOutcome<UserDetailResponse>> Patch(string id,
        PatchUserRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var userId))
        {
            return Invalid(ToErrorDictionary("id", "User id must be a valid ULID."));
        }

        if (request is null || !request.HasChanges())
        {
            return Invalid(ToErrorDictionary("body", "At least one field must be provided for patch."));
        }

        var errors = ValidatePatchUserRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return Missing(new ErrorResponse("user_not_found", "User was not found."));
        }

        DomainUser.UserRole? nextRole = null;
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!Enum.TryParse<DomainUser.UserRole>(request.Role.Trim(), true, out var parsedRole) || !Enum.IsDefined(parsedRole))
            {
                return Invalid(ToErrorDictionary("role", "Role is invalid."));
            }

            nextRole = parsedRole;
        }

        if (actor.Id == userId && ((nextRole is { } role && role != user.Role) || request.IsActive == false))
            return Conflict(new ErrorResponse("self_access_change", "You cannot revoke your own administrative access."));

        var nextEmail = request.Email is null ? user.Email : request.Email.Trim().ToLowerInvariant();
        var nextTelegramUsername = request.TelegramUsername is null ? user.TelegramUsername : request.TelegramUsername.Trim();

        if (!string.Equals(nextEmail, user.Email, StringComparison.OrdinalIgnoreCase) &&
            await dbContext.Users.AnyAsync(x => x.Id != userId && x.Email == nextEmail, cancellationToken))
        {
            return Conflict(new ErrorResponse("user_email_conflict", "Email already exists."));
        }

        if (!string.Equals(nextTelegramUsername, user.TelegramUsername, StringComparison.Ordinal) &&
            await dbContext.Users.AnyAsync(x => x.Id != userId && x.TelegramUsername == nextTelegramUsername, cancellationToken))
        {
            return Conflict(new ErrorResponse("user_telegram_conflict", "Telegram username already exists."));
        }

        dbContext.Entry(user).CurrentValues[nameof(DomainUser.Email)] = nextEmail;
        dbContext.Entry(user).CurrentValues[nameof(DomainUser.FullName)] = request.FullName?.Trim() ?? user.FullName;
        dbContext.Entry(user).CurrentValues[nameof(DomainUser.TelegramUsername)] = nextTelegramUsername;
        dbContext.Entry(user).CurrentValues[nameof(DomainUser.TelegramWork)] = request.TelegramWork is null ? user.TelegramWork : NormalizeOptional(request.TelegramWork);

        if (nextRole is not null)
        {
            dbContext.Entry(user).CurrentValues[nameof(DomainUser.Role)] = nextRole.Value;
        }

        if (request.Password is not null)
        {
            dbContext.Entry(user).CurrentValues[nameof(DomainUser.PasswordHash)] = passwordService.HashPassword(request.Password);
        }

        if (request.IsActive is not null)
        {
            if (request.IsActive.Value)
            {
                user.Activate();
            }
            else
            {
                user.Deactivate();
            }
        }

        if ((nextRole is { } changedRole && changedRole != dbContext.Entry(user).OriginalValues.GetValue<DomainUser.UserRole>(nameof(DomainUser.Role))) ||
            request.Password is not null || request.IsActive == false)
            await RevokeSessionsAsync(user.Id, cancellationToken);
        await SaveUserChangeAsync("patch", user.Id, actor.Id, cancellationToken);

        var response = await BuildDetailQuery(user.Id).FirstAsync(cancellationToken);
        return ModuleOutcome<UserDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<UserDetailResponse>> ChangeStatus(string id,
        ChangeUserStatusRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var userId))
        {
            return Invalid(ToErrorDictionary("id", "User id must be a valid ULID."));
        }

        if (request is null || request.IsActive is null)
        {
            return Invalid(ToErrorDictionary("isActive", "isActive is required."));
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return Missing(new ErrorResponse("user_not_found", "User was not found."));
        }

        if (request.IsActive.Value)
        {
            user.Activate();
        }
        else
        {
            if (actor.Id == userId)
                return Conflict(new ErrorResponse("self_access_change", "You cannot deactivate yourself."));
            user.Deactivate();
            await RevokeSessionsAsync(user.Id, cancellationToken);
        }

        await SaveUserChangeAsync("change_status", user.Id, actor.Id, cancellationToken);

        var response = await BuildDetailQuery(user.Id).FirstAsync(cancellationToken);
        return ModuleOutcome<UserDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<UserDetailResponse>> ResetPassword(string id,
        ResetUserPasswordRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var userId))
        {
            return Invalid(ToErrorDictionary("id", "User id must be a valid ULID."));
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Password))
        {
            return Invalid(ToErrorDictionary("password", "Password is required."));
        }

        if (request.Password.Trim().Length < 8)
        {
            return Invalid(ToErrorDictionary("password", "Password must be at least 8 characters long."));
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return Missing(new ErrorResponse("user_not_found", "User was not found."));
        }

        dbContext.Entry(user).CurrentValues[nameof(DomainUser.PasswordHash)] = passwordService.HashPassword(request.Password);
        await RevokeSessionsAsync(user.Id, cancellationToken);
        await SaveUserChangeAsync("reset_password", user.Id, actor.Id, cancellationToken);

        var response = await BuildDetailQuery(user.Id).FirstAsync(cancellationToken);
        return ModuleOutcome<UserDetailResponse>.Success(response);
    }
}
