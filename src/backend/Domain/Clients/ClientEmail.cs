using NUlid;

namespace backend.Domain.Clients;

public sealed class ClientEmail
{
    public Ulid Id { get; private init; } = Ulid.NewUlid();
    public Ulid ClientId { get; private init; }
    public Client Client { get; private init; } = null!;
    public Email Email { get; private set; } = null!;
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private init; } = DateTimeOffset.UtcNow;

    private ClientEmail() { }

    private ClientEmail(Ulid clientId, Email email, bool isPrimary, DateTimeOffset createdAtUtc)
    {
        ClientId = clientId;
        Email = email;
        IsPrimary = isPrimary;
        CreatedAtUtc = createdAtUtc;
    }

    public static ClientEmail Create(
        Ulid clientId,
        Email email,
        bool isPrimary = false,
        DateTimeOffset? createdAtUtc = null)
    {
        return new ClientEmail(
            clientId,
            email ?? throw new ArgumentNullException(nameof(email)),
            isPrimary,
            createdAtUtc ?? DateTimeOffset.UtcNow);
    }

    public void SetPrimary() => IsPrimary = true;

    public void UnsetPrimary() => IsPrimary = false;
}
