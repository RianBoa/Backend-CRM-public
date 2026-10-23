using backend.Features.Integrations.Bots;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace backend.Infrastructure.Messaging;

public sealed class DiscordResultConsumer(IServiceScopeFactory scopes, IOptions<BotMessagingOptions> options,
    ILogger<DiscordResultConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await RabbitBotPublisher.Factory(options.Value, "ffa-discord-results")
                    .CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await RabbitTopology.DeclareAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    if (delivery.Body.Length > 65536)
                    {
                        await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken: stoppingToken);
                        logger.LogWarning("Oversized Discord result rejected");
                        return;
                    }
                    // RabbitMQ owns the delivery buffer; do not retain it across callback completion.
                    var body = delivery.Body.ToArray();
                    var disposition = ResultDisposition.Rejected;
                    if (delivery.BasicProperties.ContentType == "application/json" && delivery.BasicProperties.Type == BotMessages.GrantResult)
                    {
                        for (var attempt = 1; attempt <= 3; attempt++)
                        {
                            try
                            {
                                await using var scope = scopes.CreateAsyncScope();
                                disposition = await scope.ServiceProvider.GetRequiredService<DiscordResultHandler>()
                                    .HandleAsync(body, delivery.BasicProperties.MessageId, stoppingToken);
                                break;
                            }
                            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                            catch (Exception error)
                            {
                                logger.LogWarning("Discord result processing failed. Attempt: {Attempt}, ErrorType: {ErrorType}",
                                    attempt, error.GetType().Name);
                                if (attempt < 3) await Task.Delay(TimeSpan.FromSeconds(attempt), stoppingToken);
                            }
                        }
                    }
                    if (disposition == ResultDisposition.Rejected)
                    {
                        await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken: stoppingToken);
                        logger.LogWarning("Discord result rejected for dead lettering");
                    }
                    else await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                };
                await channel.BasicConsumeAsync(RabbitTopology.ResultQueue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
                while (channel.IsOpen && !stoppingToken.IsCancellationRequested)
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Discord result transport disconnected. ErrorType: {ErrorType}", error.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
