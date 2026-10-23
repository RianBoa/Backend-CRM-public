

namespace backend.Domain.Applications;

using NUlid;
using backend.Domain.Users;
public sealed record ApplicationType
{
    public Ulid Id { get; private init; } = Ulid.NewUlid();
    public string Code { get; private init; } = null!;
    public string Name { get; private init; } = null!;
    public User.UserRole TargetRole { get; private init; }
    public int? SlaDays { get; private init; }
    public bool IsActive { get; private set; } = true;

    private ApplicationType() { }

    public static ApplicationType Create(string code, string name, User.UserRole targetRole, int? slaDays = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code must not be empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must not be empty.", nameof(name));

        return new ApplicationType
        {
            Code = code,
            Name = name,
            TargetRole = targetRole,
            SlaDays = slaDays
        };
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
