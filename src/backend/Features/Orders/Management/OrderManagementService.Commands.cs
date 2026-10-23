using backend.Auth;
using backend.Domain.Auditing;
using System.Text.Json;
using backend.Domain.Orders;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Orders.Management;

public sealed partial class OrderManagementService
{
    public async Task<ModuleOutcome<OrderDetailResponse>> Create(CreateOrderRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateCreateOrderRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        if (!Ulid.TryParse(request.ClientId, out var clientId))
        {
            return Invalid(ToErrorDictionary("clientId", "Client id must be a valid ULID."));
        }

        if (!Enum.TryParse<OrderSource>(request.Source, true, out var source) || !Enum.IsDefined(source))
        {
            return Invalid(ToErrorDictionary("source", "Source is invalid."));
        }

        var clientExists = await dbContext.Clients.AnyAsync(client => client.Id == clientId, cancellationToken);
        if (!clientExists)
        {
            return Missing(new ErrorResponse("client_not_found", "Client was not found."));
        }

        var order = Order.Create(clientId, source);
        dbContext.Orders.Add(order);

        var orderItems = request.Items!;
        var productIds = orderItems.Select(item => item.ProductId).Distinct().ToList();
        var packageIds = orderItems.Select(item => item.PackageId).Distinct().ToList();
        var products = await dbContext.Products.Where(product => productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        var packages = await dbContext.Packages.Where(package => packageIds.Contains(package.Id)).ToDictionaryAsync(package => package.Id, cancellationToken);

        foreach (var item in orderItems)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                return Missing(new ErrorResponse("product_not_found", $"Product {item.ProductId} was not found."));
            }

            if (!packages.TryGetValue(item.PackageId, out var package))
            {
                return Missing(new ErrorResponse("package_not_found", $"Package {item.PackageId} was not found."));
            }

            if (package.ProductId != product.Id)
            {
                return Conflict(new ErrorResponse("package_product_mismatch", $"Package {package.Id} does not belong to product {product.Id}."));
            }

            dbContext.OrderItems.Add(OrderItem.Create(order, product, package, item.Price, item.Quantity));
        }

        foreach (var payment in request.Payments ?? [])
        {
            dbContext.Payments.Add(Payment.Create(
                order.Id,
                payment.Amount,
                payment.Currency!.Trim(),
                payment.Status?.Trim() ?? "Pending",
                payment.TotalInstallments ?? 1,
                payment.CurrentInstallment,
                payment.AmountUA,
                payment.ExchangeRate,
                NormalizeOptional(payment.Provider),
                NormalizeOptional(payment.Invoice),
                payment.PaidAt,
                payment.PaidUntil));
        }

        dbContext.AuditLogs.Add(AuditLog.Create(actor.Id, "create", "order", order.Id.ToString(),
            JsonSerializer.Serialize(new { request.ClientId, request.Source })));
        if (!await provisioner.SaveOrderChangesAsync(order.Id, actor.Id, cancellationToken))
            return Conflict(new ErrorResponse("provisioning_conflict", "Another payment is provisioning access. Reload and retry."));

        var response = await BuildDetailAsync(order.Id, cancellationToken);
        return ModuleOutcome<OrderDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<OrderDetailResponse>> Patch(string id,
        PatchOrderRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var orderId))
        {
            return Invalid(ToErrorDictionary("id", "Order id must be a valid ULID."));
        }

        if (request is null || !request.HasChanges())
        {
            return Invalid(ToErrorDictionary("body", "At least one field must be provided for patch."));
        }

        var order = await dbContext.Orders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            return Missing(new ErrorResponse("order_not_found", "Order was not found."));
        }

        if (request.Status is not null)
        {
            try
            {
                dbContext.Entry(order).CurrentValues[nameof(Order.Status)] = OrderStatus.FromValue(request.Status.Trim());
            }
            catch (ArgumentException ex)
            {
                return Invalid(ToErrorDictionary("status", ex.Message));
            }
        }

        if (request.Source is not null)
        {
            if (!Enum.TryParse<OrderSource>(request.Source.Trim(), true, out var source) || !Enum.IsDefined(source))
            {
                return Invalid(ToErrorDictionary("source", "Source is invalid."));
            }

            dbContext.Entry(order).CurrentValues[nameof(Order.Source)] = source;
        }

        await ModuleAudit.SaveAsync(dbContext, actor.Id, "patch", "order", order.Id.ToString(), new { request.Status, request.Source }, cancellationToken);

        var response = await BuildDetailAsync(order.Id, cancellationToken);
        return ModuleOutcome<OrderDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<OrderDetailResponse>> AddPayment(string id,
        AddPaymentRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var orderId))
        {
            return Invalid(ToErrorDictionary("id", "Order id must be a valid ULID."));
        }

        var errors = ValidateAddPaymentRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var orderExists = await dbContext.Orders.AnyAsync(order => order.Id == orderId, cancellationToken);
        if (!orderExists)
        {
            return Missing(new ErrorResponse("order_not_found", "Order was not found."));
        }

        dbContext.Payments.Add(Payment.Create(
            orderId,
            request.Amount,
            request.Currency!.Trim(),
            request.Status?.Trim() ?? "Pending",
            request.TotalInstallments ?? 1,
            request.CurrentInstallment,
            request.AmountUA,
            request.ExchangeRate,
            NormalizeOptional(request.Provider),
            NormalizeOptional(request.Invoice),
            request.PaidAt,
            request.PaidUntil));

        dbContext.AuditLogs.Add(AuditLog.Create(actor.Id, "add_payment", "order", orderId.ToString(),
            JsonSerializer.Serialize(new { request.Amount, request.Currency })));
        if (!await provisioner.SaveOrderChangesAsync(orderId, actor.Id, cancellationToken))
            return Conflict(new ErrorResponse("provisioning_conflict", "Another payment is provisioning access. Reload and retry."));

        var response = await BuildDetailAsync(orderId, cancellationToken);
        return ModuleOutcome<OrderDetailResponse>.Success(response);
    }
}
