namespace ProCricketAuction.Domain.Entities;

public sealed class TournamentSeason
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public required string Name { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Venue { get; set; } = string.Empty;
    public string Status { get; set; } = "Upcoming";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Tournament? Tournament { get; set; }
    public ICollection<Team> Teams { get; set; } = [];
    public ICollection<Player> Players { get; set; } = [];
    public ICollection<Auction> AuctionEvents { get; set; } = [];
}
