namespace ProCricketAuction.Domain.Entities;

public sealed class Player
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Guid SeasonId { get; set; }
    public required string FullName { get; set; }
    public string City { get; set; } = string.Empty;
    public string Role { get; set; } = "Batter";
    public string Email { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string? DateOfBirth { get; set; }
    public string PhotoDataUrl { get; set; } = string.Empty;
    public long BasePrice { get; set; }
    public string Status { get; set; } = "Available";
    public Guid? TeamId { get; set; }
    public long? SoldPrice { get; set; }

    public Tournament? Tournament { get; set; }
    public TournamentSeason? Season { get; set; }
    public Team? Team { get; set; }
}
