using Microsoft.AspNetCore.Identity;

namespace ProCricketAuction.Domain.Entities;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
