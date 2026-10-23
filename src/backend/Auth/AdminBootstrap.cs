using backend.Domain.Auditing;
using backend.Domain.Users;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Auth;

public static class AdminBootstrap
{
    public static async Task CreateAsync(ApplicationDbContext db, IUserPasswordService passwords, IConfiguration config)
    {
        if (await db.Users.AnyAsync()) throw new InvalidOperationException("Bootstrap is only allowed for an empty user database.");
        var email = config["BootstrapAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = config["BootstrapAdmin:Password"];
        var telegram = config["BootstrapAdmin:Telegram"]?.Trim();
        var name = config["BootstrapAdmin:FullName"]?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) ||
            string.IsNullOrWhiteSpace(password) || password.Length < 12 || string.IsNullOrWhiteSpace(telegram) || string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Set BootstrapAdmin__Email, Password (12+ characters), Telegram and FullName in the environment.");
        var admin = User.Create(email, telegram, passwords.HashPassword(password), User.UserRole.Admin, name);
        db.Users.Add(admin);
        db.AuditLogs.Add(AuditLog.Create(null, "bootstrap_admin", "user", admin.Id.ToString()));
        await db.SaveChangesAsync();
    }
}
