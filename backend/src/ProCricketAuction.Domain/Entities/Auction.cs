namespace ProCricketAuction.Domain.Entities;

public sealed class Auction
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Guid SeasonId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public string Venue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Scheduled";
    public Guid? CurrentPlayerId { get; set; }
    public Guid? LeadingTeamId { get; set; }
    public long CurrentBidAmount { get; set; }
    public long BidIncrement { get; set; } = 2000;
    public int BidDurationSeconds { get; set; } = 30;
    public int BidExtensionSeconds { get; set; } = 5;
    public DateTimeOffset? BidDeadline { get; set; }
    public int? PausedRemainingMilliseconds { get; set; }
    public int MaximumSquadSize { get; set; } = 25;
    public int MinimumSquadSize { get; set; }
    public long MinimumSquadReservePrice { get; set; }
    public int CurrentRound { get; set; } = 1;
    public int MaximumRounds { get; set; } = 2;
    public int BidSequence { get; set; }
    public int EventSequence { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Tournament? Tournament { get; set; }
    public TournamentSeason? Season { get; set; }
    public ICollection<Bid> Bids { get; set; } = [];
    public ICollection<Purchase> Purchases { get; set; } = [];
    public ICollection<AuditLog> AuditEvents { get; set; } = [];
}
