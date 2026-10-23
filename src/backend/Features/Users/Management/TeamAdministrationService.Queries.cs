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
    public async Task<ModuleOutcome<PagedResponse<UserListItemResponse>>> GetList(UserListQuery query,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateQuery(query);
        if (errors.Count > 0) return Invalid(errors);

        var page = query.Page is > 0 ? query.Page.Value : DefaultPage;
        var pageSize = query.PageSize is > 0 ? Math.Min(query.PageSize.Value, MaxPageSize) : DefaultPageSize;
        var sortBy = NormalizeSortField(query.SortBy);
        var sortDir = NormalizeSortDirection(query.SortDir);

        var usersQuery = dbContext.Users
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            usersQuery = usersQuery.Where(user =>
                EF.Functions.ILike(user.FullName, $"%{search}%") ||
                EF.Functions.ILike(user.Email, $"%{search}%") ||
                EF.Functions.ILike(user.TelegramUsername, $"%{search}%") ||
                EF.Functions.ILike(user.TelegramWork ?? string.Empty, $"%{search}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.Role) &&
            Enum.TryParse<DomainUser.UserRole>(query.Role.Trim(), true, out var role))
        {
            usersQuery = usersQuery.Where(user => user.Role == role);
        }

        if (query.IsActive is not null)
        {
            usersQuery = usersQuery.Where(user => user.IsActive == query.IsActive.Value);
        }

        usersQuery = ApplySorting(usersQuery, sortBy, sortDir);

        var total = await usersQuery.CountAsync(cancellationToken);

        var items = await usersQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new UserListItemResponse(
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
                dbContext.RoadMapAssignments.Count(assignment => assignment.UserId == user.Id)))
            .ToListAsync(cancellationToken);

        return ModuleOutcome<PagedResponse<UserListItemResponse>>.Success(new PagedResponse<UserListItemResponse>(items, total, page, pageSize));
    }

    public async Task<ModuleOutcome<SelectResponse<UserSelectItemResponse>>> GetSelect(string? search,
        string? role,
        bool? activeOnly,
        int? limit,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var validation = new ListValidation().Text(search, "search").EnumValue<DomainUser.UserRole>(role, "role");
        if (limit is < 1 or > 100) validation.Errors["limit"] = ["limit must be between 1 and 100."];
        if (validation.Errors.Count > 0) return Invalid(validation.Errors);

        var take = limit is > 0 ? Math.Min(limit.Value, 100) : 50;

        var query = dbContext.Users
            .AsNoTracking()
            .AsQueryable();

        if (activeOnly is not false)
        {
            query = query.Where(user => user.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(role) &&
            Enum.TryParse<DomainUser.UserRole>(role.Trim(), true, out var parsedRole))
        {
            query = query.Where(user => user.Role == parsedRole);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(user =>
                EF.Functions.ILike(user.FullName, $"%{normalized}%") ||
                EF.Functions.ILike(user.Email, $"%{normalized}%") ||
                EF.Functions.ILike(user.TelegramUsername, $"%{normalized}%"));
        }

        var items = await query
            .OrderBy(user => user.FullName).ThenBy(user => user.Id)
            .Take(take)
            .Select(user => new UserSelectItemResponse(
                user.Id.ToString(),
                user.FullName,
                user.Email,
                user.Role.ToString(),
                user.IsActive))
            .ToListAsync(cancellationToken);

        return ModuleOutcome<SelectResponse<UserSelectItemResponse>>.Success(new SelectResponse<UserSelectItemResponse>(items));
    }

    public ModuleOutcome<SelectResponse<RoleSelectItemResponse>> GetRoles(ModuleActor actor)
    {
        var items = Enum.GetValues<DomainUser.UserRole>()
            .Select(role => new RoleSelectItemResponse(role.ToString(), role.ToString()))
            .ToList();

        return ModuleOutcome<SelectResponse<RoleSelectItemResponse>>.Success(new SelectResponse<RoleSelectItemResponse>(items));
    }

    public async Task<ModuleOutcome<UserDetailResponse>> GetById(string id, CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var userId))
        {
            return Invalid(ToErrorDictionary("id", "User id must be a valid ULID."));
        }

        var user = await BuildDetailQuery(userId).FirstOrDefaultAsync(cancellationToken);

        return user is null
            ? Missing(new ErrorResponse("user_not_found", "User was not found."))
            : ModuleOutcome<UserDetailResponse>.Success(user);
    }
}
