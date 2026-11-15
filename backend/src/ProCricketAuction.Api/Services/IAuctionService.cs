using ProCricketAuction.Api.DTOs;

namespace ProCricketAuction.Api.Services;

public interface IAuctionService
{
    Task<AuctionStateResponse?> GetStateAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken);
    Task<AuctionStateResponse?> GetPublicStateAsync(Guid auctionId, CancellationToken cancellationToken);
    Task<AuctionStateResponse?> StartAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken);
    Task<AuctionStateResponse?> PauseAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken);
    Task<AuctionStateResponse?> ResumeAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken);
    Task<AuctionStateResponse?> PresentPlayerAsync(
        Guid userId, Guid auctionId, PresentAuctionPlayerRequest request, CancellationToken cancellationToken);
    Task<AuctionStateResponse?> NextPlayerAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken);
    Task<AuctionStateResponse?> SkipPlayerAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken);
    Task<AuctionStateResponse?> MarkSoldAsync(
        Guid userId, Guid auctionId, ConfirmAuctionSaleRequest request, CancellationToken cancellationToken);
    Task<AuctionStateResponse?> MarkUnsoldAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken);
    Task<AuctionStateResponse?> StartNextRoundAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken);
    Task<AuctionStateResponse?> PlaceBidAsync(
        Guid userId, Guid auctionId, PlaceAuctionBidRequest request, CancellationToken cancellationToken);
    Task<AuctionStateResponse?> ReverseSaleAsync(
        Guid userId, Guid auctionId, ReverseAuctionSaleRequest request, CancellationToken cancellationToken);
    Task ExpireDueAuctionsAsync(CancellationToken cancellationToken);
}
