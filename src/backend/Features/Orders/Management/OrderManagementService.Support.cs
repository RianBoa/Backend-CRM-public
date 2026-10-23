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
    private async Task<OrderDetailResponse?> BuildDetailAsync(Ulid orderId, CancellationToken ct)
    {
        var order = await dbContext.Orders.AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.Items).ThenInclude(x => x.Package)
            .Include(x => x.Payments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null) return null;

        return new OrderDetailResponse(order.Id.ToString(), order.ClientId.ToString(), order.Client.Name,
            order.Status.Value, order.Source.ToString(), order.CreatedAt,
            order.Items.OrderBy(x => x.Product.Name).ThenBy(x => x.Id)
                .Select(x => new OrderItemResponse(x.Id.ToString(), x.ProductId, x.Product.Name,
                    x.PackageId, x.Package.Name, x.Price, x.Quantity)).ToList(),
            order.Payments.OrderByDescending(x => x.PaidAt).ThenByDescending(x => x.Id)
                .Select(x => new PaymentResponse(x.Id.ToString(), x.Amount, x.Currency, x.Status,
                    x.Provider, x.Invoice, x.TotalInstallments, x.CurrentInstallment,
                    x.AmountUA, x.ExchangeRate, x.PaidAt, x.PaidUntil)).ToList());
    }

    private static IQueryable<Order> ApplySorting(IQueryable<Order> query, string sortBy, string sortDir)
    {
        var descending = string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        return sortBy switch
        {
            "status" => descending ? query.OrderByDescending(order => order.Status).ThenByDescending(order => order.Id) : query.OrderBy(order => order.Status).ThenBy(order => order.Id),
            _ => descending ? query.OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id) : query.OrderBy(order => order.CreatedAt).ThenBy(order => order.Id)
        };
    }

    private static string NormalizeSortField(string? sortBy)
        => string.IsNullOrWhiteSpace(sortBy) ? "createdAt" : (AllowedSortFields.FirstOrDefault(field => string.Equals(field, sortBy.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "createdAt");

    private static string NormalizeSortDirection(string? sortDir)
        => string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

    private static bool TryParseUlid(string? value, out Ulid ulid)
        => Ulid.TryParse(value?.Trim(), out ulid);

    private static Dictionary<string, string[]> ValidateCreateOrderRequest(CreateOrderRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            errors["clientId"] = ["Client id is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Source))
        {
            errors["source"] = ["Source is required."];
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            errors["items"] = ["At least one order item is required."];
        }

        if (request.Items is not null)
        {
            if (request.Items.Count > 100) errors["items"] = ["At most 100 items are allowed."];
            for (var i = 0; i < request.Items.Count; i++)
            {
                if (request.Items[i] is null)
                {
                    errors[$"items[{i}]"] = ["An order item cannot be null."];
                    continue;
                }
                if (request.Items[i].Price <= 0 || request.Items[i].Price > 9999999999.99m ||
                    decimal.Round(request.Items[i].Price, 2) != request.Items[i].Price)
                {
                    errors[$"items[{i}].price"] = ["Price must be positive, fit decimal(12,2), and have at most two decimal places."];
                }

                if (request.Items[i].Quantity is 0 or > 100000)
                {
                    errors[$"items[{i}].quantity"] = ["Quantity must be greater than 0."];
                }
            }
        }

        if (request.Payments is { Count: > 100 }) errors["payments"] = ["At most 100 payments are allowed."];
        if (request.Payments is not null)
            for (var i = 0; i < request.Payments.Count; i++)
                foreach (var error in ValidateAddPaymentRequest(request.Payments[i]))
                    errors[$"payments[{i}].{error.Key}"] = error.Value;

        return errors;
    }

    private static Dictionary<string, string[]> ValidateAddPaymentRequest(AddPaymentRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (request.Amount <= 0)
        {
            errors["amount"] = ["Amount must be greater than 0."];
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            errors["currency"] = ["Currency is required."];
        }

        try
        {
            Payment.ValidateTerms(request.Amount, request.Currency, request.Status ?? "Pending",
                request.TotalInstallments ?? 1, request.CurrentInstallment, request.AmountUA,
                request.ExchangeRate, request.Provider, request.Invoice, request.PaidAt, request.PaidUntil);
        }
        catch (ArgumentException error)
        {
            errors[error.ParamName ?? "payment"] = [error.Message];
        }

        return errors;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
