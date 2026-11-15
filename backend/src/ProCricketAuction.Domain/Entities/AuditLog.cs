namespace ProCricketAuction.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid AuctionId { get; set; }
    public Guid? ActorUserId { get; set; }
    public int EventSequence { get; set; }
    public required string EventType { get; set; }
    public Guid? PlayerId { get; set; }
    public Guid? TeamId { get; set; }
    public long? Amount { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public Auction? Auction { get; set; }
    public ApplicationUser? Actor { get; set; }
}
