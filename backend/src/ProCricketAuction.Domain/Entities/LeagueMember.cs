namespace ProCricketAuction.Domain.Entities;

public sealed class LeagueMember
{
    public Guid TournamentId { get; set; }
    public Guid UserId { get; set; }
    public required string Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Tournament? Tournament { get; set; }
    public ApplicationUser? User { get; set; }
}
