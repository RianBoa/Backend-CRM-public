using System.Security.Cryptography;
using System.Text;

namespace backend.Auth;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private const int TokenByteLength = 64;
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public RefreshTokenResult CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        var token = Convert.ToBase64String(bytes);
        return new RefreshTokenResult(token, ComputeHash(token), DateTimeOffset.UtcNow.Add(Lifetime));
    }

    public string ComputeHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
