namespace backend.Auth;

public sealed record AuthTokenResult(
    string AccessToken,
    DateTime ExpiresAtUtc,
    int ExpiresInSeconds);
