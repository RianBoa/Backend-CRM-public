using backend.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace backend.Auth;

public sealed class UserPasswordService : IUserPasswordService
{
    private readonly PasswordHasher<User> passwordHasher = new();

    public string HashPassword(string password)
        => passwordHasher.HashPassword(null!, password);

    public bool VerifyPassword(string? passwordHash, string password)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var result = passwordHasher.VerifyHashedPassword(null!, passwordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
