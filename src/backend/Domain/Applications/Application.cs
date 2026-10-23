using backend.Domain.Orders;
using backend.Domain.Users;
using NUlid;

namespace backend.Domain.Applications;
using backend.Domain.Clients;

public sealed record Application
{
    public Ulid Id { get; private init; } = Ulid.NewUlid();

    public Ulid TypeId { get; private init; }
    public ApplicationType Type { get; private init; } = null!;

    public Ulid AuthorId { get; private init; }
    public User Author { get; private init; } = null!;

    public Ulid? ClientId { get; private init; }
    public Client? Client { get; private init; }

    public Ulid? OrderId { get; private init; }
    public Order? Order { get; private init; }

    public string Title { get; private init; } = null!;
    public string? Description { get; private init; }
    public enum ApplicationReason
    {
        Illness,
        Refund,
        Dispute,
        Feedback,
        Vacation,
        Other
    }
    public ApplicationReason Reason { get; private init; }
    public enum ApplicationStatus
    {
        New,
        InProcess,
        RequiresClarification,
        Rejected,
        Closed
    }

    public ApplicationStatus Status { get; private set; } = ApplicationStatus.New;

    public DateTime CreatedAt { get; private init; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; private set; }
    public User? ProcessedBy { get; private set; }
    public Ulid? ProcessedById { get; private set; }
    public Guid Revision { get; private set; } = Guid.NewGuid();
    public ICollection<ApplicationReply> Replies { get; private init; } = new List<ApplicationReply>();

    private Application() { }

    public static Application Create(
        ApplicationType type,
        Ulid authorId,
        string title,
        ApplicationReason reason,
        string? description = null,
        Ulid? clientId = null,
        Ulid? orderId = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        return new Application
        {
            Type = type,
            TypeId = type.Id,
            AuthorId = authorId,
            Title = title,
            Reason = reason,
            Description = description,
            ClientId = clientId,
            OrderId = orderId
        };
    }

    public void ChangeStatus(ApplicationStatus newStatus, Ulid? actorId = null, DateTime? now = null)
    {
        if (!Enum.IsDefined(newStatus)) throw new ArgumentException("Unknown application status.", nameof(newStatus));
        if (Status == newStatus) return;
        EnsureOpen();
        if (newStatus == ApplicationStatus.New)
            throw new InvalidOperationException("An application already in processing cannot return to New.");
        Status = newStatus;
        if (newStatus is ApplicationStatus.Closed or ApplicationStatus.Rejected)
        {
            ProcessedAt = now ?? DateTime.UtcNow;
            ProcessedById = actorId;
        }
        Revision = Guid.NewGuid();
    }

    public ApplicationReply AddReply(Ulid authorId, string body, DateTime now)
    {
        EnsureOpen();
        var reply = ApplicationReply.Create(this, authorId, body, now);
        Replies.Add(reply);
        Revision = Guid.NewGuid();
        return reply;
    }

    public void RecordClientChange()
    {
        EnsureOpen();
        Revision = Guid.NewGuid();
    }

    private void EnsureOpen()
    {
        if (Status is ApplicationStatus.Closed or ApplicationStatus.Rejected)
            throw new InvalidOperationException("The application is closed. Further processing is not allowed.");
    }
}
