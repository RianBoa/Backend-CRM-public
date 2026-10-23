using NUlid;

namespace backend.Domain.Messaging;

public sealed class OutboxMessage
{
    public Ulid Id { get; private init; }
    public string CorrelationId { get; private init; } = null!;
    public string MessageType { get; private init; } = null!;
    public string PayloadJson { get; private init; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private init; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public DateTimeOffset? CanceledAtUtc { get; private set; }
    public DateTimeOffset? DeadLetteredAtUtc { get; private set; }
    public string? LastErrorCode { get; private set; }

    private OutboxMessage() { }
    public static OutboxMessage Create(Ulid id, string correlationId, string type, string json, DateTimeOffset now)
        => new() { Id = id, CorrelationId = correlationId, MessageType = type, PayloadJson = json,
            CreatedAtUtc = now, NextAttemptAtUtc = now };

    public void Published(DateTimeOffset now) { Attempts++; PublishedAtUtc = now; LastErrorCode = null; }
    public void Failed(DateTimeOffset now)
    {
        Attempts++;
        LastErrorCode = "publish_failed";
        NextAttemptAtUtc = now.AddSeconds(Math.Min(300, Math.Pow(2, Attempts) * 5));
        if (Attempts >= 5) DeadLetteredAtUtc = now;
    }
    public bool Retry(DateTimeOffset now)
    {
        if (PublishedAtUtc is not null || CanceledAtUtc is not null || DeadLetteredAtUtc is null) return false;
        Attempts = 0; DeadLetteredAtUtc = null; LastErrorCode = null; NextAttemptAtUtc = now;
        return true;
    }

    public bool CancelUnpublished(DateTimeOffset now)
    {
        if (PublishedAtUtc is not null || CanceledAtUtc is not null) return false;
        CanceledAtUtc = now;
        return true;
    }
}

public sealed class InboxReceipt
{
    public Ulid Id { get; private init; }
    public Ulid RequestId { get; private init; }
    public string Fingerprint { get; private init; } = null!;
    public DateTimeOffset ReceivedAtUtc { get; private init; }
    private InboxReceipt() { }
    public static InboxReceipt Create(Ulid id, Ulid requestId, string fingerprint, DateTimeOffset now)
        => new() { Id = id, RequestId = requestId, Fingerprint = fingerprint, ReceivedAtUtc = now };
}
