using System.ComponentModel.DataAnnotations;

namespace ProCricketAuction.Api.DTOs;

/// <summary>Identifies the team and value for one idempotent bid submission.</summary>
public sealed record PlaceAuctionBidRequest
{
    /// <summary>The team assigned to the authenticated captain.</summary>
    [Required]
    public required Guid TeamId { get; init; }

    /// <summary>The proposed virtual purse amount.</summary>
    [Range(1, 9000000000000000L)]
    public required long Amount { get; init; }

    /// <summary>A client-generated UUID reused for retries of this exact submission.</summary>
    [Required]
    public required Guid ClientRequestId { get; init; }

    /// <summary>The auction revision displayed when this bid was submitted.</summary>
    [Range(0, long.MaxValue)]
    public required long ExpectedRevision { get; init; }
}

/// <summary>Selects a registered player to present to the auction room.</summary>
public sealed record PresentAuctionPlayerRequest
{
    /// <summary>The player in this auction's season.</summary>
    [Required]
    public required Guid PlayerId { get; init; }
}

/// <summary>Confirms the sale shown by the auctioneer at one known auction revision.</summary>
public sealed record ConfirmAuctionSaleRequest
{
    /// <summary>The auction revision displayed when the sale confirmation was requested.</summary>
    [Range(0, long.MaxValue)]
    public required long ExpectedRevision { get; init; }
}

/// <summary>Explains an authorized reversal of a completed sale.</summary>
public sealed record ReverseAuctionSaleRequest
{
    /// <summary>The purchase record to reverse.</summary>
    [Required]
    public required Guid PurchaseId { get; init; }

    /// <summary>The operator's reason for correcting the completed sale.</summary>
    [Required, StringLength(500, MinimumLength = 5)]
    public required string Reason { get; init; }

    /// <summary>The auction revision displayed by the operator before confirming.</summary>
    [Range(0, long.MaxValue)]
    public required long ExpectedRevision { get; init; }
}

/// <summary>Contains the committed auction snapshot for all connected screens.</summary>
public sealed record AuctionStateResponse(
    Guid AuctionId,
    Guid TournamentId,
    Guid SeasonId,
    string TournamentName,
    string SeasonName,
    string AuctionName,
    string CurrencyCode,
    string Status,
    AuctionPlayerResponse? CurrentPlayer,
    long CurrentBidAmount,
    long NextBidAmount,
    Guid? LeadingTeamId,
    string? LeadingTeamName,
    int AcceptedBidCount,
    DateTimeOffset? BidDeadline,
    int? PausedRemainingMilliseconds,
    int BidDurationSeconds,
    int BidExtensionSeconds,
    long BidIncrement,
    int CurrentRound,
    int MaximumRounds,
    int BidSequence,
    int EventSequence,
    long Revision,
    Guid? AssignedTeamId,
    IReadOnlyList<Guid> AssignedTeamIds,
    IReadOnlyList<AuctionTeamStateResponse> Teams,
    IReadOnlyList<AuctionPlayerResponse> Players,
    IReadOnlyList<AuctionBidResponse> RecentBids,
    IReadOnlyList<AuctionBidResponse> RecentAuctionBids,
    IReadOnlyList<AuctionPurchaseResponse> RecentSales,
    IReadOnlyList<AuctionAuditEventResponse> RecentAuditEvents)
{
    public AuctionStateResponse ToPublicView()
    {
        var lastResolvedPlayerId = RecentAuditEvents
            .FirstOrDefault(item => item.EventType is "PlayerSold" or "PlayerUnsold" or "PlayerSkipped")
            ?.PlayerId;
        var publicPlayers = Players
            .Where(player => player.Id == CurrentPlayer?.Id || player.Id == lastResolvedPlayerId)
            .ToArray();

        return this with
        {
            AssignedTeamId = null,
            AssignedTeamIds = [],
            Teams = Teams.Select(team => team with { CanBid = false, MaximumAffordableBid = 0 }).ToArray(),
            Players = publicPlayers,
            RecentAuditEvents = []
        };
    }
}

/// <summary>A registered player and their current auction status.</summary>
public sealed record AuctionPlayerResponse(
    Guid Id,
    string RegistrationNumber,
    string Name,
    string Role,
    string City,
    string PhotoDataUrl,
    long BasePrice,
    string Status,
    Guid? TeamId,
    long? SoldPrice);

/// <summary>A team's confirmed budget, squad, and current bidding eligibility.</summary>
public sealed record AuctionTeamStateResponse(
    Guid Id,
    string Name,
    string ShortName,
    string DisplayName,
    string LogoDataUrl,
    string Color,
    long InitialPurse,
    long AmountSpent,
    long PurseRemaining,
    int PlayersPurchased,
    int SquadSlotsRemaining,
    long MaximumAffordableBid,
    bool CanBid);

/// <summary>An accepted bid ordered by its server-assigned sequence number.</summary>
public sealed record AuctionBidResponse(
    Guid Id,
    Guid PlayerId,
    Guid TeamId,
    string TeamName,
    string TeamShortName,
    long Amount,
    int SequenceNumber,
    DateTimeOffset CreatedAt);

/// <summary>A committed purchase, including whether it has been reversed.</summary>
public sealed record AuctionPurchaseResponse(
    Guid Id,
    Guid PlayerId,
    string PlayerName,
    Guid TeamId,
    string TeamName,
    long Price,
    int Round,
    DateTimeOffset PurchasedAt,
    bool IsReversed);

/// <summary>A durable server-ordered auction action or rejected bid attempt.</summary>
public sealed record AuctionAuditEventResponse(
    int SequenceNumber,
    string EventType,
    string Details,
    Guid? PlayerId,
    Guid? TeamId,
    long? Amount,
    DateTimeOffset CreatedAt);
