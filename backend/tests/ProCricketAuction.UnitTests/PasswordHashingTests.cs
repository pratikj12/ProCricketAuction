using Microsoft.AspNetCore.Identity;
using ProCricketAuction.Domain.Entities;
using Xunit;

namespace ProCricketAuction.UnitTests;

public sealed class PasswordHashingTests
{
    [Fact]
    public void PasswordHasherStoresOnlyAVerifiableHash()
    {
        const string password = "CricketAuction!2026";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "organizer@example.com",
            Email = "organizer@example.com",
            DisplayName = "Tournament Organizer"
        };
        var hasher = new PasswordHasher<ApplicationUser>();
        var storedValue = hasher.HashPassword(user, password);

        Assert.NotEqual(password, storedValue);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, storedValue, password));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, storedValue, "WrongPassword!2026"));
    }
}
