using System.ComponentModel.DataAnnotations;
using backend.Features.Clients.Profile;
using Xunit;

namespace Backend.Tests;

public sealed class ClientProfileTests
{
    [Fact]
    public void FutureBirthDateIsRejected()
    {
        var profile = new ClientProfile("Student", null, null, null, null,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), null);
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(profile, new ValidationContext(profile), errors, true));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(ClientProfile.BirthDate)));
    }

    [Fact]
    public void BlankNameIsRejected()
    {
        var profile = new ClientProfile(" ", null, null, null, null, null, null);
        Assert.False(Validator.TryValidateObject(profile, new ValidationContext(profile), [], true));
    }
}
