using NUlid;
using backend.Domain.Users;

namespace backend.Domain.Applications;

public sealed class ApplicationReply
{
    public Ulid Id { get; private init; } = Ulid.NewUlid();
    public Ulid ApplicationId { get; private init; }
    public Application Application { get; private init; } = null!;
    public Ulid AuthorId { get; private init; }
    public User Author { get; private init; } = null!;
    public string Body { get; private init; } = null!;
    public DateTime CreatedAt { get; private init; }

    private ApplicationReply() { }

    internal static ApplicationReply Create(Application application, Ulid authorId, string body, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 4000)
            throw new ArgumentException("Reply must contain 1 to 4000 characters.", nameof(body));
        return new() { ApplicationId = application.Id, Application = application,
            AuthorId = authorId, Body = body.Trim(), CreatedAt = now };
    }
}
