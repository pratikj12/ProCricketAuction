using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using ProCricketAuction.Api.Services;

namespace ProCricketAuction.Api.Hubs;

public sealed class AuctionHub(IAuctionService auctionService) : Hub
{
    public const string StateChangedEvent = "AuctionStateChanged";

    public static string GroupName(Guid auctionId) => $"auction:{auctionId:N}";

    public async Task JoinAuction(string auctionId)
    {
        if (Context.User?.Identity?.IsAuthenticated != true
            || !Guid.TryParse(auctionId, out var parsedAuctionId)
            || !Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            throw new HubException("A valid auction and authenticated account are required.");
        }

        var state = await auctionService.GetStateAsync(userId, parsedAuctionId, Context.ConnectionAborted);
        if (state is null)
        {
            throw new HubException("This auction does not exist or your account cannot access it.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(parsedAuctionId), Context.ConnectionAborted);
        await Clients.Caller.SendAsync(StateChangedEvent, state, Context.ConnectionAborted);
    }

    public async Task LeaveAuction(string auctionId)
    {
        if (Guid.TryParse(auctionId, out var parsedAuctionId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(parsedAuctionId), Context.ConnectionAborted);
        }
    }
}
