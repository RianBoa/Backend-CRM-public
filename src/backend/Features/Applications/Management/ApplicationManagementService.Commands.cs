using backend.Auth;
using backend.Domain.Auditing;
using System.Text.Json;
using backend.Domain.Applications;
using backend.Domain.Users;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Applications.Management;

public sealed partial class ApplicationManagementService
{
    public async Task<ModuleOutcome<ApplicationDetailResponse>> Create(CreateApplicationRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateCreateApplicationRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        if (!Ulid.TryParse(request.TypeId, out var typeId))
        {
            return Invalid(ToErrorDictionary("typeId", "Type id must be a valid ULID."));
        }

        if (!Ulid.TryParse(request.AuthorId, out var authorId))
        {
            return Invalid(ToErrorDictionary("authorId", "Author id must be a valid ULID."));
        }

        if (actor.Id != authorId) return Denied();

        if (!Enum.TryParse<Application.ApplicationReason>(request.Reason, true, out var reason) || !Enum.IsDefined(reason))
        {
            return Invalid(ToErrorDictionary("reason", "Reason is invalid."));
        }

        var type = await dbContext.ApplicationTypes.FirstOrDefaultAsync(x => x.Id == typeId, cancellationToken);
        if (type is null)
        {
            return Missing(new ErrorResponse("application_type_not_found", "Application type was not found."));
        }

        if (!type.IsActive)
            return Conflict(new ErrorResponse("inactive_application_type", "Application type is inactive."));

        var authorExists = await dbContext.Users.AnyAsync(x => x.Id == authorId && x.IsActive, cancellationToken);
        if (!authorExists)
        {
            return Missing(new ErrorResponse("author_not_found", "Author was not found."));
        }

        Ulid? clientId = null;
        if (!string.IsNullOrWhiteSpace(request.ClientId))
        {
            if (!Ulid.TryParse(request.ClientId, out var parsedClientId))
            {
                return Invalid(ToErrorDictionary("clientId", "Client id must be a valid ULID."));
            }

            var clientExists = await dbContext.Clients.AnyAsync(x => x.Id == parsedClientId, cancellationToken);
            if (!clientExists)
            {
                return Missing(new ErrorResponse("client_not_found", "Client was not found."));
            }

            clientId = parsedClientId;
        }

        Ulid? orderId = null;
        if (!string.IsNullOrWhiteSpace(request.OrderId))
        {
            if (!Ulid.TryParse(request.OrderId, out var parsedOrderId))
            {
                return Invalid(ToErrorDictionary("orderId", "Order id must be a valid ULID."));
            }

            var order = await dbContext.Orders.FirstOrDefaultAsync(x => x.Id == parsedOrderId, cancellationToken);
            if (order is null)
            {
                return Missing(new ErrorResponse("order_not_found", "Order was not found."));
            }

            if (clientId is not null && clientId != order.ClientId)
                return Conflict(new ErrorResponse("order_client_mismatch", "The order belongs to another client."));
            clientId ??= order.ClientId;

            orderId = parsedOrderId;
        }

        try
        {
            var application = Application.Create(
                type,
                authorId,
                request.Title!.Trim(),
                reason,
                NormalizeOptional(request.Description),
                clientId,
                orderId);

            dbContext.Applications.Add(application);
            dbContext.AuditLogs.Add(AuditLog.Create(actor.Id, "create", "application", application.Id.ToString(),
                JsonSerializer.Serialize(new { request.TypeId, request.AuthorId, request.Reason })));
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = await BuildDetailQuery(application.Id).FirstAsync(cancellationToken);
            return ModuleOutcome<ApplicationDetailResponse>.Success(response);
        }
        catch (ArgumentException ex)
        {
            return Invalid(ToErrorDictionary("request", ex.Message));
        }
    }

    public async Task<ModuleOutcome<ApplicationDetailResponse>> ChangeStatus(string id,
        ChangeApplicationStatusRequest request,
        backend.Features.Applications.ApplicationWorkService work,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var applicationId))
        {
            return Invalid(ToErrorDictionary("id", "Application id must be a valid ULID."));
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Status))
        {
            return Invalid(ToErrorDictionary("status", "Status is required."));
        }

        if (!Enum.TryParse<Application.ApplicationStatus>(request.Status.Trim(), true, out var status) || !Enum.IsDefined(status))
        {
            return Invalid(ToErrorDictionary("status", "Status is invalid."));
        }

        if (actor.Id is not { } actorId) return Unauthenticated();
        var result = await work.ChangeStatusAsync(applicationId, actorId, status, cancellationToken);
        if (result.Error is { } error)
            return error.Kind switch
            {
                backend.Features.Applications.WorkFailure.NotFound => Missing(new ErrorResponse(error.Code, error.Message)),
                backend.Features.Applications.WorkFailure.Forbidden => Denied(new ErrorResponse(error.Code, error.Message)),
                _ => Conflict(new ErrorResponse(error.Code, error.Message))
            };

        var response = await BuildDetailQuery(applicationId).FirstAsync(cancellationToken);
        return ModuleOutcome<ApplicationDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<ApplicationTypeItemResponse>> CreateType(CreateApplicationTypeRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateCreateApplicationTypeRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        if (!Enum.TryParse<User.UserRole>(request.TargetRole, true, out var targetRole) || !Enum.IsDefined(targetRole))
        {
            return Invalid(ToErrorDictionary("targetRole", "Target role is invalid."));
        }

        if (await dbContext.ApplicationTypes.AnyAsync(x => x.Code == request.Code!.Trim(), cancellationToken))
        {
            return Conflict(new ErrorResponse("application_type_code_conflict", "Application type code already exists."));
        }

        try
        {
            var applicationType = ApplicationType.Create(
                request.Code!.Trim(),
                request.Name!.Trim(),
                targetRole,
                request.SlaDays);

            dbContext.ApplicationTypes.Add(applicationType);
            await ModuleAudit.SaveAsync(dbContext, actor.Id, "create_type", "application_type", applicationType.Id.ToString(), new { applicationType.Code, applicationType.Name }, cancellationToken);

            return ModuleOutcome<ApplicationTypeItemResponse>.Success(new ApplicationTypeItemResponse(
                applicationType.Id.ToString(),
                applicationType.Code,
                applicationType.Name,
                applicationType.TargetRole.ToString(),
                applicationType.SlaDays,
                applicationType.IsActive));
        }
        catch (ArgumentException ex)
        {
            return Invalid(ToErrorDictionary("request", ex.Message));
        }
    }

    public async Task<ModuleOutcome<ApplicationTypeItemResponse>> ChangeTypeStatus(string id,
        ChangeApplicationTypeStatusRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var applicationTypeId))
        {
            return Invalid(ToErrorDictionary("id", "Application type id must be a valid ULID."));
        }

        if (request is null || request.IsActive is null)
        {
            return Invalid(ToErrorDictionary("isActive", "isActive is required."));
        }

        var applicationType = await dbContext.ApplicationTypes.FirstOrDefaultAsync(x => x.Id == applicationTypeId, cancellationToken);
        if (applicationType is null)
        {
            return Missing(new ErrorResponse("application_type_not_found", "Application type was not found."));
        }

        if (request.IsActive.Value)
        {
            applicationType.Activate();
        }
        else
        {
            applicationType.Deactivate();
        }

        await ModuleAudit.SaveAsync(dbContext, actor.Id, "change_type_status", "application_type", applicationType.Id.ToString(), new { request.IsActive }, cancellationToken);

        return ModuleOutcome<ApplicationTypeItemResponse>.Success(new ApplicationTypeItemResponse(
            applicationType.Id.ToString(),
            applicationType.Code,
            applicationType.Name,
            applicationType.TargetRole.ToString(),
            applicationType.SlaDays,
            applicationType.IsActive));
    }
}
