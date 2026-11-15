namespace ProCricketAuction.Domain.Entities;

public sealed class Team
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Guid SeasonId { get; set; }
    public required string Name { get; set; }
    public required string ShortName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public required string Captain { get; set; }
    public string City { get; set; } = string.Empty;
    public string LogoDataUrl { get; set; } = string.Empty;
    public long Purse { get; set; }
    public long InitialPurse { get; set; }
    public string Color { get; set; } = "#FF6A00";

    public Tournament? Tournament { get; set; }
    public TournamentSeason? Season { get; set; }
}
