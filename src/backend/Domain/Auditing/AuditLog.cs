using NUlid;

namespace backend.Domain.Auditing;

public sealed class AuditLog
{
    public Ulid Id { get; private init; } = Ulid.NewUlid();
    public Ulid? UserId { get; private init; }
    public Users.User? User { get; private init; }
    public string Action { get; private init; } = null!;
    public string EntityType { get; private init; } = null!;
    public string? EntityId { get; private init; }
    public string? MetadataJson { get; private init; }
    public DateTimeOffset CreatedAtUtc { get; private init; } = DateTimeOffset.UtcNow;

    private AuditLog() { }

    public static AuditLog Create(
        Ulid? userId,
        string action,
        string entityType,
        string? entityId = null,
        string? metadataJson = null)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action is required.", nameof(action));

        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("Entity type is required.", nameof(entityType));

        return new AuditLog
        {
            UserId = userId,
            Action = action.Trim(),
            EntityType = entityType.Trim(),
            EntityId = string.IsNullOrWhiteSpace(entityId) ? null : entityId.Trim(),
            MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson
        };
    }
}
