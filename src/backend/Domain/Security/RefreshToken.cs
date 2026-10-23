using NUlid;

namespace backend.Domain.Security;

public sealed class RefreshToken
{
    public Ulid Id { get; private init; } = Ulid.NewUlid();
    public Ulid UserId { get; private init; }
    public long SecurityVersion { get; private init; }
    public Users.User User { get; private init; } = null!;
    public string TokenHash { get; private init; } = null!;
    public DateTimeOffset ExpiresAtUtc { get; private init; }
    public DateTimeOffset CreatedAtUtc { get; private init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string? UserAgent { get; private init; }
    public string? IpAddress { get; private init; }

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTimeOffset.UtcNow;

    private RefreshToken() { }

    public static RefreshToken Create(
        Ulid userId,
        string tokenHash,
        DateTimeOffset expiresAtUtc,
        string? userAgent = null,
        string? ipAddress = null,
        long securityVersion = 0)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));

        return new RefreshToken
        {
            UserId = userId,
            SecurityVersion = securityVersion,
            TokenHash = tokenHash.Trim(),
            ExpiresAtUtc = expiresAtUtc,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim(),
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim()
        };
    }

    public void Revoke(DateTimeOffset revokedAtUtc, string? replacedByTokenHash = null)
    {
        RevokedAtUtc = revokedAtUtc;
        ReplacedByTokenHash = string.IsNullOrWhiteSpace(replacedByTokenHash) ? null : replacedByTokenHash.Trim();
    }
}
