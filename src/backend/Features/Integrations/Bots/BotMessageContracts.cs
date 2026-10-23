using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace backend.Features.Integrations.Bots;

public static class BotMessages
{
    public const string GrantRequested = "discord.role.grant.requested";
    public const string GrantResult = "discord.role.grant.result";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };
}

public sealed record MessageEnvelope<T>(string MessageId, string CorrelationId, string Type,
    int Version, DateTimeOffset OccurredAtUtc, T Payload);

public sealed record GrantDiscordRole(string RequestId, string SubscriptionId, string ClientId,
    string DiscordUserId, string GuildId, string RoleId);

// Workers return "granted", "already_granted", or "failed"; never a free-form exception.
public sealed record DiscordRoleResult(string RequestId, string DiscordUserId, string GuildId,
    string RoleId, string Outcome, DateTimeOffset? ConfirmedAtUtc, string? ErrorCode);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestDiscordGrant(
    [Required, RegularExpression("^[0-9A-HJKMNP-TV-Z]{26}$")] string RequestId,
    [Required, RegularExpression("^[1-9][0-9]{16,19}$")] string DiscordUserId);

public sealed record DiscordGrantView(string RequestId, string SubscriptionId, string DiscordUserId,
    string Status, string DeliveryStatus, int Attempts, string? ErrorCode,
    DateTimeOffset RequestedAtUtc, DateTimeOffset? CompletedAtUtc);

// Reserved inbound boundary for signal reporting, not a command to place a trade.
public sealed record TradingSignalObserved(string SignalId, string Symbol, string Side, string Timeframe,
    decimal? Entry, decimal? StopLoss, decimal? TakeProfit, DateTimeOffset ObservedAtUtc);
