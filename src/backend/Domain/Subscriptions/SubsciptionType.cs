namespace backend.Domain.Subscriptions
{
    public sealed record SubscriptionType(string Value)
    {
        public static readonly SubscriptionType DiscordStandalone = new("DiscordStandalone");
        public static readonly SubscriptionType DiscordCourseBonus = new("DiscordCourseBonus");
        public static readonly SubscriptionType CoursePlatform = new("CoursePlatform");
        public static readonly SubscriptionType PackageAddon = new("PackageAddon");

        public override string ToString() => Value;
    }
}
