namespace backend.Auth;

public interface IUserPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string? passwordHash, string password);
}
