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
    public async Task<ModuleOutcome<CohortDetailResponse>> CreateCohort(CreateCohortRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateCreateCohortRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var productExists = await dbContext.Products.AnyAsync(product => product.Id == request.ProductId, cancellationToken);
        if (!productExists)
        {
            return Missing(new ErrorResponse("product_not_found", "Product was not found."));
        }

        var cohort = Cohort.Create(request.ProductId, request.Name!.Trim(), request.StartAt, request.EndsAt!.Value);
        dbContext.Cohorts.Add(cohort);
        await ModuleAudit.SaveAsync(dbContext, actor.Id, "create", "cohort", cohort.Id.ToString(), new { request.ProductId, request.Name }, cancellationToken);

        var response = await BuildCohortDetail(cohort.Id, cancellationToken);
        return ModuleOutcome<CohortDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<CohortDetailResponse>> PatchCohort(string id,
        PatchCohortRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!Ulid.TryParse(id, out var cohortId))
        {
            return Invalid(ToErrorDictionary("id", "Cohort id must be a valid ULID."));
        }

        if (request is null || !request.HasChanges())
        {
            return Invalid(ToErrorDictionary("body", "At least one field must be provided for patch."));
        }

        var cohort = await dbContext.Cohorts.FirstOrDefaultAsync(x => x.Id == cohortId, cancellationToken);
        if (cohort is null)
        {
            return Missing(new ErrorResponse("cohort_not_found", "Cohort was not found."));
        }

        if ((request.StartAt ?? cohort.StartAt) >= (request.EndsAt ?? cohort.EndsAt))
            return Invalid(ToErrorDictionary("endsAt", "EndsAt must be later than StartAt."));

        if (request.ProductId is not null)
        {
            if (request.ProductId != cohort.ProductId &&
                (await dbContext.Groups.AnyAsync(x => x.CohortId == cohortId, cancellationToken) ||
                 await dbContext.Enrollments.AnyAsync(x => x.CohortId == cohortId, cancellationToken)))
                return Conflict(new ErrorResponse("cohort_has_learning_links",
                    "Move existing groups and enrollments before changing the cohort product."));

            var productExists = await dbContext.Products.AnyAsync(product => product.Id == request.ProductId.Value, cancellationToken);
            if (!productExists)
            {
                return Missing(new ErrorResponse("product_not_found", "Product was not found."));
            }

            dbContext.Entry(cohort).CurrentValues[nameof(Cohort.ProductId)] = request.ProductId.Value;
        }

        if (request.Name is not null)
        {
            dbContext.Entry(cohort).CurrentValues[nameof(Cohort.Name)] = request.Name.Trim();
        }

        if (request.StartAt is not null)
        {
            dbContext.Entry(cohort).CurrentValues[nameof(Cohort.StartAt)] = request.StartAt.Value;
        }

        if (request.EndsAt is not null)
        {
            dbContext.Entry(cohort).CurrentValues[nameof(Cohort.EndsAt)] = request.EndsAt.Value;
        }

        await ModuleAudit.SaveAsync(dbContext, actor.Id, "patch", "cohort", cohort.Id.ToString(), new { request.Name, request.ProductId, request.StartAt, request.EndsAt }, cancellationToken);

        var response = await BuildCohortDetail(cohort.Id, cancellationToken);
        return ModuleOutcome<CohortDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<CohortDetailResponse>> ChangeCohortStatus(string id,
        ChangeCohortStatusRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!Ulid.TryParse(id, out var cohortId))
        {
            return Invalid(ToErrorDictionary("id", "Cohort id must be a valid ULID."));
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Status))
        {
            return Invalid(ToErrorDictionary("status", "Status is required."));
        }

        if (!Enum.TryParse<Cohort.CohortStatus>(request.Status.Trim(), true, out var status) || !Enum.IsDefined(status))
        {
            return Invalid(ToErrorDictionary("status", "Status is invalid."));
        }

        var cohort = await dbContext.Cohorts.FirstOrDefaultAsync(x => x.Id == cohortId, cancellationToken);
        if (cohort is null)
        {
            return Missing(new ErrorResponse("cohort_not_found", "Cohort was not found."));
        }

        cohort.SetStatus(status);
        await ModuleAudit.SaveAsync(dbContext, actor.Id, "change_status", "cohort", cohort.Id.ToString(), new { request.Status }, cancellationToken);

        var response = await BuildCohortDetail(cohort.Id, cancellationToken);
        return ModuleOutcome<CohortDetailResponse>.Success(response);
    }
}
