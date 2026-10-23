using backend.Auth;
using backend.Domain.LearningStructure;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Learning.Management;

public sealed partial class LearningAdministrationService
{
    public async Task<ModuleOutcome<GroupListItemResponse>> CreateGroup(CreateGroupRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateCreateGroupRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        if (!Ulid.TryParse(request.CohortId, out var cohortId))
        {
            return Invalid(ToErrorDictionary("cohortId", "Cohort id must be a valid ULID."));
        }

        var cohort = await dbContext.Cohorts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cohortId, cancellationToken);
        if (cohort is null)
        {
            return Missing(new ErrorResponse("cohort_not_found", "Cohort was not found."));
        }

        if (request.PackageId is not null)
        {
            var package = await dbContext.Packages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.PackageId.Value, cancellationToken);
            if (package is null)
            {
                return Missing(new ErrorResponse("package_not_found", "Package was not found."));
            }
            if (cohort.ProductId is { } productId && package.ProductId != productId)
                return Conflict(new ErrorResponse("package_product_mismatch", "Package does not belong to the cohort product."));
        }

        Ulid? mentorId = null;
        if (!string.IsNullOrWhiteSpace(request.MentorId))
        {
            if (!Ulid.TryParse(request.MentorId, out var parsedMentorId))
            {
                return Invalid(ToErrorDictionary("mentorId", "Mentor id must be a valid ULID."));
            }

            var mentorExists = await dbContext.Users.AnyAsync(x => x.Id == parsedMentorId, cancellationToken);
            if (!mentorExists)
            {
                return Missing(new ErrorResponse("mentor_not_found", "Mentor was not found."));
            }

            mentorId = parsedMentorId;
        }

        var group = Group.Create(cohortId, request.Name!.Trim(), mentorId, request.PackageId);
        if (!string.IsNullOrWhiteSpace(request.LearningPlatformId))
        {
            group.LinkLearningPlatformGroup(request.LearningPlatformId);
        }

        dbContext.Groups.Add(group);
        await ModuleAudit.SaveAsync(dbContext, actor.Id, "create", "group", group.Id.ToString(), new { request.CohortId, request.Name, request.MentorId }, cancellationToken);

        var response = await BuildGroupListItem(group.Id, cancellationToken);
        return ModuleOutcome<GroupListItemResponse>.Success(response);
    }

    public async Task<ModuleOutcome<GroupListItemResponse>> PatchGroup(string id,
        PatchGroupRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        await using var write = await backend.Features.Learning.LearningWriteScope.BeginAsync(dbContext, cancellationToken);
        if (!Ulid.TryParse(id, out var groupId))
        {
            return Invalid(ToErrorDictionary("id", "Group id must be a valid ULID."));
        }

        if (request is null || !request.HasChanges())
        {
            return Invalid(ToErrorDictionary("body", "At least one field must be provided for patch."));
        }

        var group = await dbContext.Groups.Include(x => x.Cohort).FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken);
        if (group is null)
        {
            return Missing(new ErrorResponse("group_not_found", "Group was not found."));
        }

        if (request.Name is not null)
        {
            group.ChangeName(request.Name.Trim());
        }

        if (request.MentorId is not null)
        {
            if (!Ulid.TryParse(request.MentorId, out var mentorId))
            {
                return Invalid(ToErrorDictionary("mentorId", "Mentor id must be a valid ULID."));
            }

            var mentorExists = await dbContext.Users.AnyAsync(x => x.Id == mentorId, cancellationToken);
            if (!mentorExists)
            {
                return Missing(new ErrorResponse("mentor_not_found", "Mentor was not found."));
            }

            group.AssignMentor(mentorId);
        }

        if (request.PackageId is not null)
        {
            var package = await dbContext.Packages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.PackageId.Value, cancellationToken);
            if (package is null)
            {
                return Missing(new ErrorResponse("package_not_found", "Package was not found."));
            }
            if (group.Cohort.ProductId is { } productId && package.ProductId != productId)
                return Conflict(new ErrorResponse("package_product_mismatch", "Package does not belong to the cohort product."));

            if (request.PackageId != group.PackageId && await dbContext.Enrollments.AnyAsync(x =>
                x.GroupId == groupId && (x.OrderItemId == null || x.OrderItem!.PackageId != package.Id), cancellationToken))
                return Conflict(new ErrorResponse("group_has_incompatible_students",
                    "Move or reconcile existing students before changing this group's package."));

            group.ChangePackage(request.PackageId);
        }

        if (request.LearningPlatformId is not null)
        {
            group.LinkLearningPlatformGroup(request.LearningPlatformId);
        }

        await ModuleAudit.SaveAsync(dbContext, actor.Id, "patch", "group", group.Id.ToString(), new { request.Name, request.MentorId, request.PackageId }, cancellationToken);

        var response = await BuildGroupListItem(group.Id, cancellationToken);
        await write.CommitAsync(cancellationToken);
        return ModuleOutcome<GroupListItemResponse>.Success(response);
    }
}
