namespace ProCricketAuction.Domain.Entities;

public sealed class Bid
{
    public Guid Id { get; set; }
    public Guid AuctionId { get; set; }
    public Guid? PlayerId { get; set; }
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
    public Guid ClientRequestId { get; set; }
    public long Amount { get; set; }
    public int SequenceNumber { get; set; }
    public bool Accepted { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Auction? Auction { get; set; }
    public Player? Player { get; set; }
    public Team? Team { get; set; }
    public ApplicationUser? User { get; set; }
}
