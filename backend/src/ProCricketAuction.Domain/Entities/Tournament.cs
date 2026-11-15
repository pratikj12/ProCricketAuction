namespace ProCricketAuction.Domain.Entities;

public sealed class Tournament
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public required string Name { get; set; }
    public string Season { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public long DefaultTeamPurse { get; set; } = 100000;
    public string CurrencyCode { get; set; } = "INR";
    public int? MaximumTeams { get; set; }
    public int? MaximumPlayers { get; set; }
    public string Status { get; set; } = "Upcoming";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationUser? Owner { get; set; }
    public ICollection<TournamentSeason> Seasons { get; set; } = [];
    public ICollection<Auction> AuctionEvents { get; set; } = [];
    public ICollection<LeagueMember> Members { get; set; } = [];
    public ICollection<Team> Teams { get; set; } = [];
    public ICollection<Player> Players { get; set; } = [];
}
