namespace backend.Auth;

public interface IRefreshTokenService
{
    RefreshTokenResult CreateToken();
    string ComputeHash(string token);
}
