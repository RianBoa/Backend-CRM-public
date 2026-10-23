namespace backend.Auth;

public sealed record RefreshTokenResult(
    string PlainTextToken,
    string TokenHash,
    DateTimeOffset ExpiresAtUtc);
