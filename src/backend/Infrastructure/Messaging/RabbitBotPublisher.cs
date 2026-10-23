using System.Text;
using backend.Domain.Messaging;
using backend.Features.Integrations.Bots;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace backend.Infrastructure.Messaging;

public sealed class RabbitBotPublisher(IOptions<BotMessagingOptions> options) : IBotMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection? connection;
    private IChannel? channel;

    public async Task PublishAsync(OutboxMessage message, CancellationToken ct)
    {
        if (message.MessageType != BotMessages.GrantRequested)
            throw new InvalidOperationException("No routing contract exists for this message type.");
        await gate.WaitAsync(ct);
        try
        {
            if (connection is not { IsOpen: true })
            {
                if (channel is not null) await channel.DisposeAsync();
                channel = null;
                if (connection is not null) await connection.DisposeAsync();
                connection = await Factory(options.Value, "ffa-outbox").CreateConnectionAsync(ct);
            }
            if (channel is not { IsOpen: true })
            {
                if (channel is not null) await channel.DisposeAsync();
                channel = await connection.CreateChannelAsync(new CreateChannelOptions(
                    publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
                await RabbitTopology.DeclareAsync(channel, ct);
            }
            var properties = new BasicProperties { ContentType = "application/json", Persistent = true,
                MessageId = message.Id.ToString(), CorrelationId = message.CorrelationId, Type = message.MessageType };
            // Await the mandatory publisher confirmation, not just the socket write.
            await channel.BasicPublishAsync(RabbitTopology.Commands, RabbitTopology.CommandKey,
                mandatory: true, basicProperties: properties, body: Encoding.UTF8.GetBytes(message.PayloadJson), cancellationToken: ct);
        }
        finally { gate.Release(); }
    }

    internal static ConnectionFactory Factory(BotMessagingOptions settings, string name) => new()
    {
        Uri = new Uri(settings.ConnectionString), ClientProvidedName = name,
        AutomaticRecoveryEnabled = true, NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
    };

    public async ValueTask DisposeAsync()
    {
        if (channel is not null) await channel.DisposeAsync();
        if (connection is not null) await connection.DisposeAsync();
        gate.Dispose();
    }
}
