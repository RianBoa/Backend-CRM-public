namespace backend.Controllers;

public sealed partial class AuthController
{
    public sealed record LoginRequest(
        [System.ComponentModel.DataAnnotations.StringLength(254)] string? Email,
        [System.ComponentModel.DataAnnotations.StringLength(1024)] string? Password);
    public sealed record RefreshRequest([System.ComponentModel.DataAnnotations.StringLength(512)] string? RefreshToken);
    public sealed record LogoutRequest([System.ComponentModel.DataAnnotations.StringLength(512)] string? RefreshToken);

    public sealed record AuthenticatedUserResponse(
        string Id,
        string ShortId,
        string FullName,
        string Email,
        string TelegramUsername,
        string? TelegramWork,
        string Role,
        bool IsActive,
        IReadOnlyList<string> Permissions);

    public sealed record LoginResponse(
        string AccessToken,
        string RefreshToken,
        string TokenType,
        DateTime ExpiresAtUtc,
        int ExpiresInSeconds,
        AuthenticatedUserResponse User);

    public sealed record MeResponse(AuthenticatedUserResponse User);
}
