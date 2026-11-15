namespace ProCricketAuction.Domain.Entities;

public sealed class TeamMember
{
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
}
