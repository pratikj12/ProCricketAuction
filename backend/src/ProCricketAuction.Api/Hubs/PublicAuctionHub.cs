using Microsoft.AspNetCore.SignalR;
using ProCricketAuction.Api.Services;

namespace ProCricketAuction.Api.Hubs;

public sealed class PublicAuctionHub(IAuctionService auctionService) : Hub
{
    public async Task JoinPublicAuction(string auctionId)
    {
        if (!Guid.TryParse(auctionId, out var parsedAuctionId))
        {
            throw new HubException("A valid auction is required.");
        }

        var state = await auctionService.GetPublicStateAsync(parsedAuctionId, Context.ConnectionAborted);
        if (state is null)
        {
            throw new HubException("This auction is not available for public viewing.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId, GroupName(parsedAuctionId), Context.ConnectionAborted);
        await Clients.Caller.SendAsync(
            AuctionHub.StateChangedEvent, state, Context.ConnectionAborted);
    }

    public Task LeavePublicAuction(string auctionId) =>
        Guid.TryParse(auctionId, out var parsedAuctionId)
            ? Groups.RemoveFromGroupAsync(
                Context.ConnectionId, GroupName(parsedAuctionId), Context.ConnectionAborted)
            : Task.CompletedTask;

    public static string GroupName(Guid auctionId) => $"public-auction:{auctionId:N}";
}
