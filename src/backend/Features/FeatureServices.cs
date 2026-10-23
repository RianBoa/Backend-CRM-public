using Microsoft.Extensions.DependencyInjection.Extensions;

namespace backend.Features;

public static class FeatureServices
{
    public static IServiceCollection AddBackendFeatures(this IServiceCollection services)
    {
        services.AddScoped<Products.Management.CatalogService>();
        services.AddScoped<Orders.Management.OrderManagementService>();
        services.AddScoped<Cases.Management.CaseManagementService>();
        services.AddScoped<Users.Management.TeamAdministrationService>();
        services.AddScoped<Applications.Management.ApplicationManagementService>();
        services.AddScoped<Learning.Management.LearningAdministrationService>();
        services.AddScoped<RoadMaps.Management.RoadMapAdministrationService>();
        services.AddScoped<Subscriptions.Management.SubscriptionManagementService>();
        services.AddScoped<Clients.Profile.ClientProfileService>();
        services.AddScoped<Clients.Contacts.ClientContactsService>();
        services.AddScoped<Clients.Financials.ClientFinancialsService>();
        services.AddScoped<Clients.Purchases.ClientPurchaseQueries>();
        services.AddScoped<Clients.Directory.ClientDirectoryQueries>();
        services.AddScoped<Clients.Workspace.ClientDetailQueries>();
        services.AddScoped<Clients.Workspace.ClientProductQueries>();
        services.AddScoped<Clients.Workspace.ClientActivityQueries>();
        services.AddScoped<Clients.Workspace.ClientHistoryQueries>();
        services.AddScoped<Clients.Commands.ClientWriteService>();
        services.AddScoped<Subscriptions.OrderSubscriptionProvisioner>();
        services.AddScoped<Subscriptions.AccessActivationService>();
        services.AddScoped<Subscriptions.SubscriptionCancellationService>();
        services.AddScoped<Applications.ApplicationWorkService>();
        services.AddScoped<RoadMaps.StudentRoadMapService>();
        services.AddScoped<Learning.EnrollmentService>();
        services.AddScoped<Learning.LearningDirectory>();
        services.AddScoped<Integrations.Sales.SalesClientResolver>();
        services.AddScoped<Integrations.Sales.SalesCatalogResolver>();
        services.AddScoped<Integrations.Sales.SalesOrderImportService>();
        services.AddOptions<Integrations.Bots.BotMessagingOptions>();
        services.AddScoped<Integrations.Bots.DiscordGrantService>();
        services.AddScoped<Integrations.Bots.DiscordResultHandler>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
