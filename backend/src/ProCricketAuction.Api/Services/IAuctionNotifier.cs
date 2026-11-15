using ProCricketAuction.Api.DTOs;

namespace ProCricketAuction.Api.Services;

public interface IAuctionNotifier
{
    Task PublishAsync(Guid auctionId, AuctionStateResponse state, CancellationToken cancellationToken);
}
