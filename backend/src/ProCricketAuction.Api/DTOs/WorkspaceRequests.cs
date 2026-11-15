using System.ComponentModel.DataAnnotations;

namespace ProCricketAuction.Api.DTOs;

/// <summary>Fields used to create or update a tournament.</summary>
public sealed record TournamentRequest
{
    /// <summary>The tournament's display name.</summary>
    [Required, StringLength(160, MinimumLength = 2)]
    public required string Name { get; init; }

    /// <summary>The season label shown to organizers.</summary>
    [StringLength(40)]
    public string Season { get; init; } = string.Empty;

    /// <summary>The tournament venue or city.</summary>
    [StringLength(160)]
    public string Venue { get; init; } = string.Empty;

    /// <summary>The starting purse assigned to each team added to this tournament.</summary>
    [Range(1, 9000000000000000L)]
    public long DefaultTeamPurse { get; init; } = 100000;

    /// <summary>The league's ISO 4217 currency code.</summary>
    [RegularExpression("^(INR|USD|GBP|EUR|AED)$")]
    public string CurrencyCode { get; init; } = "INR";

    /// <summary>The optional maximum number of teams allowed in a season.</summary>
    [Range(1, 100)]
    public int? MaximumTeams { get; init; }

    /// <summary>The optional maximum number of players allowed in a season.</summary>
    [Range(1, 10000)]
    public int? MaximumPlayers { get; init; }
}

/// <summary>Fields used to create a team within a tournament.</summary>
public sealed record CreateTeamRequest
{
    /// <summary>The full team name.</summary>
    [Required, StringLength(100, MinimumLength = 2)]
    public required string Name { get; init; }

    /// <summary>The short code displayed on scoreboards and auction screens.</summary>
    [Required, StringLength(8, MinimumLength = 2), RegularExpression("^[A-Za-z0-9]+$")]
    public required string ShortName { get; init; }

    /// <summary>An optional name shown in auction displays.</summary>
    [StringLength(100)]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>The displayed captain name.</summary>
    [Required, StringLength(100, MinimumLength = 2)]
    public required string Captain { get; init; }

    /// <summary>The team's home city or region.</summary>
    [StringLength(120)]
    public string City { get; init; } = string.Empty;

    /// <summary>A resized JPEG, PNG, or WebP team logo data URL.</summary>
    [RegularExpression("^$|^data:image/(png|jpeg|webp);base64,[A-Za-z0-9+/]+={0,2}$"), StringLength(350000)]
    public string LogoDataUrl { get; init; } = string.Empty;

    /// <summary>The season this team belongs to, or the league's first season.</summary>
    public Guid? SeasonId { get; init; }
}

/// <summary>Fields used to register a player in a tournament.</summary>
public sealed record CreatePlayerRequest
{
    /// <summary>The player's full name.</summary>
    [Required, StringLength(120, MinimumLength = 2)]
    public required string Name { get; init; }

    /// <summary>The season this player belongs to, or the league's first season.</summary>
    public Guid? SeasonId { get; init; }

    /// <summary>The player's city or region.</summary>
    [StringLength(120)]
    public string City { get; init; } = string.Empty;

    /// <summary>The player's email address for registration records.</summary>
    [EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    /// <summary>The player's nationality.</summary>
    [StringLength(80)]
    public string Nationality { get; init; } = string.Empty;

    /// <summary>The player's date of birth in ISO yyyy-MM-dd format.</summary>
    [RegularExpression("^$|^\\d{4}-\\d{2}-\\d{2}$")]
    public string? DateOfBirth { get; init; }

    /// <summary>A resized JPEG, PNG, or WebP player photo data URL.</summary>
    [RegularExpression("^$|^data:image/(png|jpeg|webp);base64,[A-Za-z0-9+/]+={0,2}$"), StringLength(700000)]
    public string PhotoDataUrl { get; init; } = string.Empty;

    /// <summary>The player's cricketing role.</summary>
    [Required, RegularExpression("^(Batter|Bowler|All-Rounder|Wicket-Keeper)$")]
    public required string Role { get; init; }

    /// <summary>The player's minimum opening bid in whole currency units.</summary>
    [Range(0, 9000000000000000L)]
    public required long BasePrice { get; init; }

    /// <summary>The auction status to preserve when importing an existing workspace.</summary>
    [RegularExpression("^(Available|Sold|Unsold)$")]
    public string Status { get; init; } = "Available";

    /// <summary>The existing owning team when importing a sold player.</summary>
    public Guid? TeamId { get; init; }

    /// <summary>The confirmed sale price for an imported sold player.</summary>
    [Range(0, 9000000000000000L)]
    public long? SoldPrice { get; init; }
}

/// <summary>A complete legacy browser workspace imported transactionally for its owner.</summary>
public sealed record ImportWorkspaceRequest
{
    /// <summary>The tournament to import.</summary>
    [Required]
    public required TournamentRequest Tournament { get; init; }

    /// <summary>Teams and their current and initial purses.</summary>
    public IReadOnlyList<ImportTeamRequest> Teams { get; init; } = [];

    /// <summary>Players and any existing sale assignments.</summary>
    public IReadOnlyList<ImportPlayerRequest> Players { get; init; } = [];
}

/// <summary>A team from a browser workspace being imported.</summary>
public sealed record ImportTeamRequest
{
    /// <summary>The previous browser identifier used to link imported records.</summary>
    [Required, StringLength(80)]
    public required string LegacyId { get; init; }

    /// <summary>The full team name.</summary>
    [Required, StringLength(100, MinimumLength = 2)]
    public required string Name { get; init; }

    /// <summary>The short team code, unique within the tournament.</summary>
    [Required, StringLength(8, MinimumLength = 2)]
    public required string ShortName { get; init; }

    /// <summary>The optional display name shown on auction screens.</summary>
    [StringLength(100)]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>The displayed captain name.</summary>
    [Required, StringLength(100, MinimumLength = 2)]
    public required string Captain { get; init; }

    /// <summary>The team's home city or region.</summary>
    [StringLength(120)]
    public string City { get; init; } = string.Empty;

    /// <summary>A resized JPEG, PNG, or WebP team logo data URL.</summary>
    [RegularExpression("^$|^data:image/(png|jpeg|webp);base64,[A-Za-z0-9+/]+={0,2}$"), StringLength(350000)]
    public string LogoDataUrl { get; init; } = string.Empty;

    /// <summary>The starting auction purse in whole currency units.</summary>
    [Range(1, 9000000000000000L)]
    public required long InitialPurse { get; init; }

    /// <summary>The unspent purse in whole currency units.</summary>
    [Range(0, 9000000000000000L)]
    public required long Purse { get; init; }

    /// <summary>The team's visual color as a hexadecimal CSS color.</summary>
    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$")]
    public required string Color { get; init; }
}

/// <summary>A player from a browser workspace being imported.</summary>
public sealed record ImportPlayerRequest
{
    /// <summary>The previous browser identifier used to link imported records.</summary>
    [Required, StringLength(80)]
    public required string LegacyId { get; init; }

    /// <summary>The player's full name.</summary>
    [Required, StringLength(120, MinimumLength = 2)]
    public required string Name { get; init; }

    /// <summary>The player's city or region.</summary>
    [StringLength(120)]
    public string City { get; init; } = string.Empty;

    /// <summary>The player's email address for registration records.</summary>
    [EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    /// <summary>The player's nationality.</summary>
    [StringLength(80)]
    public string Nationality { get; init; } = string.Empty;

    /// <summary>The player's date of birth in ISO yyyy-MM-dd format.</summary>
    [RegularExpression("^$|^\\d{4}-\\d{2}-\\d{2}$")]
    public string? DateOfBirth { get; init; }

    /// <summary>A resized JPEG, PNG, or WebP player photo data URL.</summary>
    [RegularExpression("^$|^data:image/(png|jpeg|webp);base64,[A-Za-z0-9+/]+={0,2}$"), StringLength(700000)]
    public string PhotoDataUrl { get; init; } = string.Empty;

    /// <summary>The player's cricketing role.</summary>
    [Required, RegularExpression("^(Batter|Bowler|All-Rounder|Wicket-Keeper)$")]
    public required string Role { get; init; }

    /// <summary>The player's minimum opening bid in whole currency units.</summary>
    [Range(0, 9000000000000000L)]
    public required long BasePrice { get; init; }

    /// <summary>The player's existing auction status.</summary>
    [RegularExpression("^(Available|Sold|Unsold)$")]
    public string Status { get; init; } = "Available";

    /// <summary>The legacy team identifier for a sold player.</summary>
    [StringLength(80)]
    public string? TeamLegacyId { get; init; }

    /// <summary>The player's existing sale price, if sold.</summary>
    [Range(0, 9000000000000000L)]
    public long? SoldPrice { get; init; }
}

/// <summary>A tournament and its related teams and players.</summary>
public sealed record TournamentWorkspaceResponse(
    TournamentResponse Tournament,
    IReadOnlyList<SeasonResponse> Seasons,
    IReadOnlyList<TeamResponse> Teams,
    IReadOnlyList<PlayerResponse> Players,
    IReadOnlyList<AuctionEventResponse> AuctionEvents);

/// <summary>Fields used to create or update a season within a league.</summary>
public sealed record SeasonRequest
{
    /// <summary>The season label shown to organizers.</summary>
    [Required, StringLength(80, MinimumLength = 2)]
    public required string Name { get; init; }

    /// <summary>The scheduled season start date.</summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>The scheduled season end date.</summary>
    public DateOnly? EndDate { get; init; }

    /// <summary>The venue or city for the season.</summary>
    [StringLength(160)]
    public string Venue { get; init; } = string.Empty;

    /// <summary>The season state.</summary>
    [RegularExpression("^(Upcoming|Active|Completed)$")]
    public string Status { get; init; } = "Upcoming";
}

/// <summary>Selected source records to copy into a target tournament season as a fresh auction roster.</summary>
public sealed record CopyRosterRequest
{
    /// <summary>The tournament containing the selected source records.</summary>
    [Required]
    public required Guid SourceTournamentId { get; init; }

    /// <summary>The season containing the selected source records.</summary>
    [Required]
    public required Guid SourceSeasonId { get; init; }

    /// <summary>Selected source team identifiers.</summary>
    public IReadOnlyList<Guid> TeamIds { get; init; } = [];

    /// <summary>Selected source player identifiers.</summary>
    public IReadOnlyList<Guid> PlayerIds { get; init; } = [];
}

/// <summary>The number of teams and players copied into a season.</summary>
public sealed record CopyRosterResponse(int TeamsCopied, int PlayersCopied);

/// <summary>A season owned by a league.</summary>
public sealed record SeasonResponse(
    Guid Id,
    Guid TournamentId,
    string Name,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Venue,
    string Status,
    int TeamCount,
    int PlayerCount,
    int SoldPlayerCount,
    int AvailablePlayerCount,
    long TotalSpend,
    DateTimeOffset CreatedAt);

/// <summary>A tournament owned by the authenticated organizer.</summary>
public sealed record TournamentResponse(
    Guid Id,
    string Name,
    string Season,
    string Venue,
    long DefaultTeamPurse,
    string CurrencyCode,
    int? MaximumTeams,
    int? MaximumPlayers,
    string Status,
    DateTimeOffset CreatedAt);

/// <summary>Fields used to schedule or update an auction event.</summary>
public sealed record AuctionEventRequest
{
    /// <summary>The season containing this auction event.</summary>
    [Required]
    public required Guid SeasonId { get; init; }

    /// <summary>The event's display name.</summary>
    [Required, StringLength(120, MinimumLength = 2)]
    public required string Name { get; init; }

    /// <summary>The date and time at which the event is scheduled.</summary>
    [Required]
    public required DateTimeOffset ScheduledAt { get; init; }

    /// <summary>The event venue or meeting location.</summary>
    [StringLength(160)]
    public string Venue { get; init; } = string.Empty;

    /// <summary>Additional organizer notes.</summary>
    [StringLength(1000)]
    public string Description { get; init; } = string.Empty;

    /// <summary>The event state.</summary>
    [RegularExpression("^(Scheduled|Cancelled)$")]
    public string Status { get; init; } = "Scheduled";

    /// <summary>The minimum amount by which an accepted bid must increase.</summary>
    [Range(1, 9000000000000000L)]
    public long BidIncrement { get; init; } = 2000;

    /// <summary>The server-controlled bidding window for each player.</summary>
    [Range(10, 300)]
    public int BidDurationSeconds { get; init; } = 30;

    /// <summary>Seconds added when a bid arrives close to the deadline.</summary>
    [Range(0, 60)]
    public int BidExtensionSeconds { get; init; } = 5;

    /// <summary>The maximum number of purchased players allowed on one team.</summary>
    [Range(1, 100)]
    public int MaximumSquadSize { get; init; } = 25;

    /// <summary>The optional minimum squad size used by the advisory affordability calculation.</summary>
    [Range(0, 100)]
    public int MinimumSquadSize { get; init; }

    /// <summary>The minimum reserve amount used for each unfilled minimum squad slot.</summary>
    [Range(0, 9000000000000000L)]
    public long MinimumSquadReservePrice { get; init; }

    /// <summary>The maximum number of auction rounds, including later rounds for unsold players.</summary>
    [Range(1, 10)]
    public int MaximumRounds { get; init; } = 2;
}

/// <summary>An auction event scheduled for a league season.</summary>
public sealed record AuctionEventResponse(
    Guid Id,
    Guid TournamentId,
    Guid SeasonId,
    string Name,
    DateTimeOffset ScheduledAt,
    string Venue,
    string Description,
    string Status,
    DateTimeOffset CreatedAt,
    long BidIncrement,
    int BidDurationSeconds,
    int BidExtensionSeconds,
    int MaximumSquadSize,
    int MinimumSquadSize,
    long MinimumSquadReservePrice,
    int MaximumRounds);

/// <summary>Fields used to add an existing account to a league.</summary>
public sealed record AddLeagueMemberRequest
{
    /// <summary>The email address of an account that has already registered.</summary>
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }

    /// <summary>The member's league role.</summary>
    [Required, RegularExpression("^(Administrator|Auctioneer|Captain|Viewer)$")]
    public required string Role { get; init; }

    /// <summary>Teams assigned to this member, used to authorize captain bids.</summary>
    public IReadOnlyList<Guid> TeamIds { get; init; } = [];
}

/// <summary>Fields used to update a league member's role.</summary>
public sealed record UpdateLeagueMemberRoleRequest
{
    /// <summary>The member's league role.</summary>
    [Required, RegularExpression("^(Administrator|Auctioneer|Captain|Viewer)$")]
    public required string Role { get; init; }

    /// <summary>Teams assigned to this member, used to authorize captain bids.</summary>
    public IReadOnlyList<Guid> TeamIds { get; init; } = [];
}

/// <summary>A registered account assigned to a league.</summary>
public sealed record LeagueMemberResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    IReadOnlyList<Guid> TeamIds,
    DateTimeOffset AddedAt);

/// <summary>A team registered to a tournament.</summary>
public sealed record TeamResponse(
    Guid Id,
    Guid TournamentId,
    Guid SeasonId,
    string Name,
    string ShortName,
    string DisplayName,
    string Captain,
    string City,
    string LogoDataUrl,
    long Purse,
    long InitialPurse,
    string Color);

/// <summary>A player registered to a tournament.</summary>
public sealed record PlayerResponse(
    Guid Id,
    Guid TournamentId,
    Guid SeasonId,
    string Name,
    string City,
    string Email,
    string Nationality,
    string? DateOfBirth,
    string PhotoDataUrl,
    string Role,
    long BasePrice,
    string Status,
    Guid? TeamId,
    long? SoldPrice);
