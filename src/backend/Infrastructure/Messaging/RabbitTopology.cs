using RabbitMQ.Client;

namespace backend.Infrastructure.Messaging;

internal static class RabbitTopology
{
    public const string Commands = "ffa.bot.commands";
    public const string Results = "ffa.bot.results";
    public const string DeadLetters = "ffa.bot.dead-letter";
    public const string CommandQueue = "ffa.discord.commands.v1";
    public const string ResultQueue = "ffa.discord.results.v1";
    public const string CommandKey = "discord.role.grant";
    public const string ResultKey = "discord.role.grant.result";

    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        foreach (var exchange in new[] { Commands, Results, DeadLetters })
            await channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
        await QueueAsync(channel, CommandQueue, Commands, CommandKey, "discord.commands.failed", ct);
        await QueueAsync(channel, ResultQueue, Results, ResultKey, "discord.results.failed", ct);
        const string deadQueue = "ffa.discord.dead-letter.v1";
        await channel.QueueDeclareAsync(deadQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: ct);
        foreach (var key in new[] { "discord.commands.failed", "discord.results.failed" })
            await channel.QueueBindAsync(deadQueue, DeadLetters, key, cancellationToken: ct);
    }

    private static async Task QueueAsync(IChannel channel, string queue, string exchange, string key, string deadKey, CancellationToken ct)
    {
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum", ["x-delivery-limit"] = 5,
                ["x-dead-letter-strategy"] = "at-least-once", ["x-overflow"] = "reject-publish",
                ["x-dead-letter-exchange"] = DeadLetters, ["x-dead-letter-routing-key"] = deadKey }, cancellationToken: ct);
        await channel.QueueBindAsync(queue, exchange, key, cancellationToken: ct);
    }
}
