using backend.Auth;
using backend.Domain.Cases;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Cases.Management;

public sealed partial class CaseManagementService
{
    public async Task<ModuleOutcome<CaseDetailResponse>> Create(CreateCaseRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateCreateCaseRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        if (!Ulid.TryParse(request.ClientId, out var clientId))
        {
            return Invalid(ToErrorDictionary("clientId", "Client id must be a valid ULID."));
        }

        if (!Ulid.TryParse(request.CreatedById, out var createdById))
        {
            return Invalid(ToErrorDictionary("createdById", "CreatedBy id must be a valid ULID."));
        }

        if (actor.Id != createdById) return Denied();

        if (!Enum.TryParse<Case.CaseType>(request.Type, true, out var type) || !Enum.IsDefined(type))
        {
            return Invalid(ToErrorDictionary("type", "Type is invalid."));
        }

        var clientExists = await dbContext.Clients.AnyAsync(x => x.Id == clientId, cancellationToken);
        if (!clientExists)
        {
            return Missing(new ErrorResponse("client_not_found", "Client was not found."));
        }

        var userExists = await dbContext.Users.AnyAsync(x => x.Id == createdById, cancellationToken);
        if (!userExists)
        {
            return Missing(new ErrorResponse("created_by_not_found", "CreatedBy user was not found."));
        }

        try
        {
            var @case = Case.Create(
                clientId,
                createdById,
                type,
                request.Amount,
                request.Currency ?? "USD",
                request.ScreenshotUrl,
                request.TelegramUrl,
                request.Comment,
                request.IsFeatured ?? false,
                request.HasConsent ?? true);

            dbContext.Cases.Add(@case);
            await ModuleAudit.SaveAsync(dbContext, actor.Id, "create", "case", @case.Id.ToString(), new { request.ClientId, request.CreatedById, request.Type }, cancellationToken);

            var response = await BuildDetailQuery(@case.Id, actor.CanViewFinancials).FirstAsync(cancellationToken);
            return ModuleOutcome<CaseDetailResponse>.Success(response);
        }
        catch (ArgumentException ex)
        {
            return Invalid(ToErrorDictionary("request", ex.Message));
        }
    }

    public async Task<ModuleOutcome<CaseDetailResponse>> ChangeStatus(string id,
        ChangeCaseStatusRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var caseId))
        {
            return Invalid(ToErrorDictionary("id", "Case id must be a valid ULID."));
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Status))
        {
            return Invalid(ToErrorDictionary("status", "Status is required."));
        }

        if (!Enum.TryParse<Case.CaseStatus>(request.Status.Trim(), true, out var status) || !Enum.IsDefined(status))
        {
            return Invalid(ToErrorDictionary("status", "Status is invalid."));
        }

        var @case = await dbContext.Cases.FirstOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (@case is null)
        {
            return Missing(new ErrorResponse("case_not_found", "Case was not found."));
        }

        @case.ChangeStatus(status);
        await ModuleAudit.SaveAsync(dbContext, actor.Id, "change_status", "case", @case.Id.ToString(), new { request.Status }, cancellationToken);

        var response = await BuildDetailQuery(@case.Id, actor.CanViewFinancials).FirstAsync(cancellationToken);
        return ModuleOutcome<CaseDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<CaseDetailResponse>> ChangeFeatured(string id,
        ChangeCaseFeaturedRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var caseId))
        {
            return Invalid(ToErrorDictionary("id", "Case id must be a valid ULID."));
        }

        if (request is null || request.IsFeatured is null)
        {
            return Invalid(ToErrorDictionary("isFeatured", "isFeatured is required."));
        }

        var @case = await dbContext.Cases.FirstOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (@case is null)
        {
            return Missing(new ErrorResponse("case_not_found", "Case was not found."));
        }

        @case.SetFeatured(request.IsFeatured.Value);
        await ModuleAudit.SaveAsync(dbContext, actor.Id, "change_featured", "case", @case.Id.ToString(), new { request.IsFeatured }, cancellationToken);

        var response = await BuildDetailQuery(@case.Id, actor.CanViewFinancials).FirstAsync(cancellationToken);
        return ModuleOutcome<CaseDetailResponse>.Success(response);
    }
}
