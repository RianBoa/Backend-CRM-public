namespace backend.Features.Integrations.Bots;

public sealed class BotMessagingOptions
{
    public bool Enabled { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public Dictionary<string, DiscordRoleTarget> DiscordPackages { get; set; } = new(StringComparer.Ordinal);
}

public sealed class DiscordRoleTarget
{
    public string GuildId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
}
