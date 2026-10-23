using backend.Features.Integrations.Bots;
using Microsoft.Extensions.Options;

namespace backend.Infrastructure.Messaging;

public static class BotMessagingRegistration
{
    public static IServiceCollection AddBotMessaging(this IServiceCollection services, IConfiguration configuration, bool runWorkers)
    {
        services.AddOptions<BotMessagingOptions>().Bind(configuration.GetSection("BotMessaging"))
            .Validate(x => !x.Enabled || Uri.TryCreate(x.ConnectionString, UriKind.Absolute, out var uri) &&
                uri.Scheme is "amqp" or "amqps", "Enabled bot messaging requires an AMQP connection string.")
            .ValidateOnStart();
        services.AddSingleton<IBotMessagePublisher, RabbitBotPublisher>();
        services.AddScoped<OutboxDispatcher>();
        if (runWorkers && configuration.GetValue<bool>("BotMessaging:Enabled"))
        {
            services.AddHostedService<OutboxWorker>();
            services.AddHostedService<DiscordResultConsumer>();
        }
        return services;
    }
}
