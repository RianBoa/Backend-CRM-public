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

public sealed partial class OrderManagementService(ApplicationDbContext dbContext,
    backend.Features.Subscriptions.OrderSubscriptionProvisioner provisioner)
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private static readonly string[] AllowedSortFields = ["createdAt", "status"];
}
