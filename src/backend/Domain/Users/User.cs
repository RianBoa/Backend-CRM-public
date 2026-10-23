using NUlid;

namespace backend.Domain.Users
{
    public sealed class User
    {
        public Ulid Id { get; private init; } = Ulid.NewUlid();
        public string ShortId => Id.ToString()[..10];
        public string Email { get; private set; } = null!;
        public string FullName { get; private init; }
        public string TelegramUsername { get; private set; } = null!;
        public string? TelegramWork { get; private set; }
        public string? PasswordHash { get; private set; } = null!;
        public long SecurityVersion { get; private set; }

        public void InvalidateSessions() => SecurityVersion = checked(SecurityVersion + 1);

        public UserRole Role { get; private set; }

        public bool IsActive { get; private set; } = true;
        public DateTime CreatedAt { get; private init; } = DateTime.UtcNow;

        private User() { }

        public static User Create(
            string email,
            string telegramUsername,
            string passwordHash,
            UserRole role,
            string fullName,
            string? telegramWork = null)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty.", nameof(email));

            if (string.IsNullOrWhiteSpace(telegramUsername))
                throw new ArgumentException("Telegram username cannot be empty.", nameof(telegramUsername));

            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));

            return new User
            {
                Email = email,
                FullName =  fullName,
                TelegramUsername = telegramUsername,
                TelegramWork = telegramWork,
                PasswordHash = passwordHash,
                Role = role
            };
        }

        public enum UserRole
        {
            Admin,
            OnBoarding,
            Curator,
            ProjectManager,
            TroubleShooter,
            Analyst,
            TeamLead,
            Developer,
        }

        public void Deactivate() => IsActive = false;
        public void Activate() => IsActive = true;
    }
}
