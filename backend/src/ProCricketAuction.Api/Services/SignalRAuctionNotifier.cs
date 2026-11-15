using Microsoft.AspNetCore.SignalR;
using ProCricketAuction.Api.DTOs;
using ProCricketAuction.Api.Hubs;

namespace ProCricketAuction.Api.Services;

public sealed class SignalRAuctionNotifier(
    IHubContext<AuctionHub> hub,
    ILogger<SignalRAuctionNotifier> logger) : IAuctionNotifier
{
    public async Task PublishAsync(
        Guid auctionId,
        AuctionStateResponse state,
        CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.Group(AuctionHub.GroupName(auctionId))
                .SendAsync(AuctionHub.StateChangedEvent, state, cancellationToken);
            await hub.Clients.Group(PublicAuctionHub.GroupName(auctionId))
                .SendAsync(AuctionHub.StateChangedEvent, state.ToPublicView(), cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or OperationCanceledException)
        {
            logger.LogWarning(exception,
                "Auction {AuctionId} was committed, but notifying connected clients failed; clients will recover from the state endpoint.",
                auctionId);
        }
    }
}
