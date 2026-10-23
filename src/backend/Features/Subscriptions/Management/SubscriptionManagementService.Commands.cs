using backend.Auth;
using backend.Domain.Subscriptions;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Subscriptions.Management;

public sealed partial class SubscriptionManagementService
{
    public async Task<ModuleOutcome<SubscriptionDetailResponse>> Create(CreateSubscriptionRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!actor.CanOverrideSubscriptions)
            return Denied(new ErrorResponse("subscription_override_forbidden", "Only an administrator can create a manual subscription."));

        var errors = ValidateCreateSubscriptionRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        if (!Ulid.TryParse(request.ClientId, out var clientId))
        {
            return Invalid(ToErrorDictionary("clientId", "Client id must be a valid ULID."));
        }

        if (!Ulid.TryParse(request.OrderItemId, out var orderItemId))
        {
            return Invalid(ToErrorDictionary("orderItemId", "Order item id must be a valid ULID."));
        }

        if (!Enum.TryParse<Subscription.SubscriptionType>(request.Type, true, out var type) || !Enum.IsDefined(type))
        {
            return Invalid(ToErrorDictionary("type", "Type is invalid."));
        }

        if (!Enum.TryParse<Subscription.SubscriptionStatus>(request.Status, true, out var status) || !Enum.IsDefined(status))
        {
            return Invalid(ToErrorDictionary("status", "Status is invalid."));
        }

        var product = await dbContext.Products.FirstOrDefaultAsync(x => x.Id == request.ProductId, cancellationToken);
        if (product is null)
        {
            return Missing(new ErrorResponse("product_not_found", "Product was not found."));
        }

        var orderItem = await dbContext.OrderItems.Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.Id == orderItemId, cancellationToken);
        if (orderItem is null)
        {
            return Missing(new ErrorResponse("order_item_not_found", "Order item was not found."));
        }

        var clientExists = await dbContext.Clients.AnyAsync(x => x.Id == clientId, cancellationToken);
        if (!clientExists)
        {
            return Missing(new ErrorResponse("client_not_found", "Client was not found."));
        }

        try
        {
            var subscription = Subscription.Create(
                clientId,
                product,
                orderItem,
                request.StartAt,
                request.EndAt,
                type,
                status,
                NormalizeOptional(request.Notes));

            dbContext.Subscriptions.Add(subscription);
            await ModuleAudit.SaveAsync(dbContext, actor.Id, "create", "subscription", subscription.Id.ToString(), new { request.ClientId, request.OrderItemId, request.Type, request.Status }, cancellationToken);

            var response = await BuildDetailQuery(subscription.Id).FirstAsync(cancellationToken);
            return ModuleOutcome<SubscriptionDetailResponse>.Success(response);
        }
        catch (ArgumentException ex)
        {
            return Invalid(ToErrorDictionary("request", ex.Message));
        }
    }

    public async Task<ModuleOutcome<SubscriptionDetailResponse>> Patch(string id,
        PatchSubscriptionRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var subscriptionId))
        {
            return Invalid(ToErrorDictionary("id", "Subscription id must be a valid ULID."));
        }

        if (request is null || !request.HasChanges())
        {
            return Invalid(ToErrorDictionary("body", "At least one field must be provided for patch."));
        }

        if ((request.StartAt is not null || request.EndAt is not null) && !actor.CanOverrideSubscriptions)
            return Denied(new ErrorResponse("subscription_override_forbidden", "Only an administrator can change manual subscription dates."));

        if (request.EndAt is not null && request.StartAt is not null && request.EndAt <= request.StartAt)
        {
            return Invalid(ToErrorDictionary("endAt", "EndAt must be later than StartAt."));
        }

        var subscription = await dbContext.Subscriptions.FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken);
        if (subscription is null)
        {
            return Missing(new ErrorResponse("subscription_not_found", "Subscription was not found."));
        }

        var nextStartAt = request.StartAt ?? subscription.StartAt;
        var nextEndAt = request.EndAt ?? subscription.EndAt;

        if (subscription.ProvisioningKey is not null && (request.StartAt is not null || request.EndAt is not null))
            return Conflict(new ErrorResponse("managed_subscription", "Automatically provisioned subscription dates are managed by the access lifecycle."));

        if (nextEndAt <= nextStartAt)
        {
            return Invalid(ToErrorDictionary("endAt", "EndAt must be later than StartAt."));
        }

        dbContext.Entry(subscription).CurrentValues[nameof(Subscription.StartAt)] = nextStartAt;
        dbContext.Entry(subscription).CurrentValues[nameof(Subscription.EndAt)] = nextEndAt;
        dbContext.Entry(subscription).CurrentValues[nameof(Subscription.Notes)] = request.Notes is null ? subscription.Notes : NormalizeOptional(request.Notes);

        await ModuleAudit.SaveAsync(dbContext, actor.Id, "patch", "subscription", subscription.Id.ToString(), new { request.StartAt, request.EndAt }, cancellationToken);

        var response = await BuildDetailQuery(subscription.Id).FirstAsync(cancellationToken);
        return ModuleOutcome<SubscriptionDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<SubscriptionDetailResponse>> ChangeStatus(string id,
        ChangeSubscriptionStatusRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var subscriptionId))
        {
            return Invalid(ToErrorDictionary("id", "Subscription id must be a valid ULID."));
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Status))
        {
            return Invalid(ToErrorDictionary("status", "Status is required."));
        }

        if (!Enum.TryParse<Subscription.SubscriptionStatus>(request.Status.Trim(), true, out var status) || !Enum.IsDefined(status))
        {
            return Invalid(ToErrorDictionary("status", "Status is invalid."));
        }

        var subscription = await dbContext.Subscriptions.FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken);
        if (subscription is null)
        {
            return Missing(new ErrorResponse("subscription_not_found", "Subscription was not found."));
        }

        if (status == Subscription.SubscriptionStatus.AwaitingAccess ||
            subscription.Status == Subscription.SubscriptionStatus.AwaitingAccess ||
            (subscription.ProvisioningKey is not null && status == Subscription.SubscriptionStatus.Active))
            return Conflict(new ErrorResponse("managed_subscription", "Use activate-access for the initial grant or resume-access to restore suspended access."));

        if (status == Subscription.SubscriptionStatus.Active)
        {
            if (subscription.Status == Subscription.SubscriptionStatus.Suspended)
                return Conflict(new ErrorResponse("use_resume_access", "Use resume-access to restore a suspended subscription without changing its term."));
            if (!actor.CanOverrideSubscriptions)
                return Denied(new ErrorResponse("subscription_override_forbidden", "Only an administrator can override a manual subscription status."));
        }

        bool changed;
        try { changed = subscription.ChangeStatus(status, DateTime.UtcNow); }
        catch (InvalidOperationException ex) { return Conflict(new("subscription_transition_conflict", ex.Message)); }
        if (changed)
            await ModuleAudit.SaveAsync(dbContext, actor.Id, "change_status", "subscription", subscription.Id.ToString(), new { request.Status }, cancellationToken);

        var response = await BuildDetailQuery(subscription.Id).FirstAsync(cancellationToken);
        return ModuleOutcome<SubscriptionDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<SubscriptionDetailResponse>> AddDiscordAccess(string id,
        AddDiscordAccessRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var subscriptionId))
        {
            return Invalid(ToErrorDictionary("id", "Subscription id must be a valid ULID."));
        }

        if (request is null ||
            string.IsNullOrWhiteSpace(request.DiscordUserId) ||
            string.IsNullOrWhiteSpace(request.RoleId) ||
            string.IsNullOrWhiteSpace(request.RoleName))
        {
            return Invalid(ToErrorDictionary("request", "Discord user id, role id, and role name are required."));
        }

        var subscriptionExists = await dbContext.Subscriptions.AnyAsync(x => x.Id == subscriptionId, cancellationToken);
        if (!subscriptionExists)
        {
            return Missing(new ErrorResponse("subscription_not_found", "Subscription was not found."));
        }

        dbContext.DiscordAccesses.Add(DiscordAccess.Create(
            subscriptionId,
            request.DiscordUserId.Trim(),
            request.RoleId.Trim(),
            request.RoleName.Trim(),
            request.GrantedAt));

        await ModuleAudit.SaveAsync(dbContext, actor.Id, "add_discord_access", "subscription", subscriptionId.ToString(), new { request.DiscordUserId, request.RoleId }, cancellationToken);

        var response = await BuildDetailQuery(subscriptionId).FirstAsync(cancellationToken);
        return ModuleOutcome<SubscriptionDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<SubscriptionDetailResponse>> AddPlatformAccess(string id,
        AddPlatformAccessRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var subscriptionId))
        {
            return Invalid(ToErrorDictionary("id", "Subscription id must be a valid ULID."));
        }

        if (request is null ||
            string.IsNullOrWhiteSpace(request.PlatformName) ||
            string.IsNullOrWhiteSpace(request.PlatformUserId))
        {
            return Invalid(ToErrorDictionary("request", "Platform name and platform user id are required."));
        }

        var subscriptionExists = await dbContext.Subscriptions.AnyAsync(x => x.Id == subscriptionId, cancellationToken);
        if (!subscriptionExists)
        {
            return Missing(new ErrorResponse("subscription_not_found", "Subscription was not found."));
        }

        dbContext.PlatformAccesses.Add(PlatformAccess.Create(
            subscriptionId,
            request.PlatformName.Trim(),
            request.PlatformUserId.Trim(),
            request.GrantedAt,
            NormalizeOptional(request.Notes)));

        await ModuleAudit.SaveAsync(dbContext, actor.Id, "add_platform_access", "subscription", subscriptionId.ToString(), new { request.PlatformName, request.PlatformUserId }, cancellationToken);

        var response = await BuildDetailQuery(subscriptionId).FirstAsync(cancellationToken);
        return ModuleOutcome<SubscriptionDetailResponse>.Success(response);
    }
}
