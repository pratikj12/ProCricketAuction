namespace ProCricketAuction.Domain.Entities;

public sealed class Purchase
{
    public Guid Id { get; set; }
    public Guid AuctionId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid TeamId { get; set; }
    public long Price { get; set; }
    public int Round { get; set; }
    public DateTimeOffset PurchasedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsReversed { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
    public string? CorrectionReason { get; set; }

    public Auction? Auction { get; set; }
    public Player? Player { get; set; }
    public Team? Team { get; set; }
}
