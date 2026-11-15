using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using ProCricketAuction.Api.DTOs;
using ProCricketAuction.Api.Services;

namespace ProCricketAuction.Api.Endpoints;

public static class AuctionEndpoints
{
    public static IEndpointRouteBuilder MapAuctionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auctions = endpoints.MapGroup("/api/v1/auctions").RequireAuthorization();

        endpoints.MapGet("/api/v1/public/auctions/{auctionId:guid}", async Task<Results<
                Ok<AuctionStateResponse>,
                NotFound>> (
                Guid auctionId,
                IAuctionService service,
                CancellationToken cancellationToken) =>
            {
                var state = await service.GetPublicStateAsync(auctionId, cancellationToken);
                return state is null ? TypedResults.NotFound() : TypedResults.Ok(state);
            })
            .AllowAnonymous()
            .WithName("GetPublicAuctionState")
            .WithSummary("Get the read-only public display state for a started auction")
            .Produces<AuctionStateResponse>()
            .Produces(StatusCodes.Status404NotFound);

        auctions.MapGet("/{auctionId:guid}/state", async Task<Results<
                Ok<AuctionStateResponse>,
                NotFound,
                UnauthorizedHttpResult>> (
                Guid auctionId,
                ClaimsPrincipal principal,
                IAuctionService service,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetUserId(principal, out var userId))
                {
                    return TypedResults.Unauthorized();
                }
                var state = await service.GetStateAsync(userId, auctionId, cancellationToken);
                return state is null ? TypedResults.NotFound() : TypedResults.Ok(state);
            })
            .WithName("GetAuctionState")
            .WithSummary("Get the authenticated league member's authoritative auction state")
            .Produces<AuctionStateResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        auctions.MapPost("/{auctionId:guid}/bids", async Task<Results<
                Ok<AuctionStateResponse>,
                NotFound,
                Conflict<ProblemDetails>,
                ForbidHttpResult,
                UnauthorizedHttpResult>> (
                Guid auctionId,
                PlaceAuctionBidRequest request,
                ClaimsPrincipal principal,
                IAuctionService service,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetUserId(principal, out var userId))
                {
                    return TypedResults.Unauthorized();
                }
                try
                {
                    var state = await service.PlaceBidAsync(userId, auctionId, request, cancellationToken);
                    return state is null ? TypedResults.NotFound() : TypedResults.Ok(state);
                }
                catch (AuctionPermissionException)
                {
                    return TypedResults.Forbid();
                }
                catch (AuctionCommandRejectedException exception)
                {
                    return TypedResults.Conflict(new ProblemDetails
                    {
                        Title = "Bid rejected",
                        Detail = exception.Message,
                        Status = StatusCodes.Status409Conflict
                    });
                }
            })
            .WithName("PlaceAuctionBid")
            .WithSummary("Submit an idempotent captain bid validated against current server state")
            .Produces<AuctionStateResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        MapControl(auctions, "/{auctionId:guid}/start", "StartAuction", "Start a scheduled auction",
            (service, user, auction, ct) => service.StartAsync(user, auction, ct));
        MapControl(auctions, "/{auctionId:guid}/pause", "PauseAuction", "Pause bidding and persist the remaining timer",
            (service, user, auction, ct) => service.PauseAsync(user, auction, ct));
        MapControl(auctions, "/{auctionId:guid}/resume", "ResumeAuction", "Resume bidding using the persisted timer remainder",
            (service, user, auction, ct) => service.ResumeAsync(user, auction, ct));
        MapControl(auctions, "/{auctionId:guid}/players/next", "PresentNextAuctionPlayer",
            "Present the next eligible player or complete the round",
            (service, user, auction, ct) => service.NextPlayerAsync(user, auction, ct));
        MapControl(auctions, "/{auctionId:guid}/players/skip", "SkipAuctionPlayer",
            "Mark the active player unsold and advance to the next player",
            (service, user, auction, ct) => service.SkipPlayerAsync(user, auction, ct));
        auctions.MapPost("/{auctionId:guid}/sold", async Task<Results<
                Ok<AuctionStateResponse>,
                NotFound,
                Conflict<ProblemDetails>,
                ForbidHttpResult,
                UnauthorizedHttpResult>> (
                Guid auctionId,
                ConfirmAuctionSaleRequest request,
                ClaimsPrincipal principal,
                IAuctionService service,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(principal, userId => service.MarkSoldAsync(
                userId, auctionId, request, cancellationToken)))
            .WithName("ConfirmAuctionSale")
            .WithSummary("Confirm the reviewed auction revision as a purchase in one transaction")
            .Produces<AuctionStateResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
        MapControl(auctions, "/{auctionId:guid}/unsold", "MarkAuctionPlayerUnsold",
            "Mark the active player unsold without deducting purse",
            (service, user, auction, ct) => service.MarkUnsoldAsync(user, auction, ct));
        MapControl(auctions, "/{auctionId:guid}/rounds/next", "StartNextAuctionRound",
            "Open the next configured round for remaining unsold players",
            (service, user, auction, ct) => service.StartNextRoundAsync(user, auction, ct));

        auctions.MapPost("/{auctionId:guid}/players/present", async Task<Results<
                Ok<AuctionStateResponse>,
                NotFound,
                Conflict<ProblemDetails>,
                ForbidHttpResult,
                UnauthorizedHttpResult>> (
                Guid auctionId,
                PresentAuctionPlayerRequest request,
                ClaimsPrincipal principal,
                IAuctionService service,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(principal, userId => service.PresentPlayerAsync(
                userId, auctionId, request, cancellationToken)));

        auctions.MapPost("/{auctionId:guid}/corrections/reverse-sale", async Task<Results<
                Ok<AuctionStateResponse>,
                NotFound,
                Conflict<ProblemDetails>,
                ForbidHttpResult,
                UnauthorizedHttpResult>> (
                Guid auctionId,
                ReverseAuctionSaleRequest request,
                ClaimsPrincipal principal,
                IAuctionService service,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(principal, userId => service.ReverseSaleAsync(
                userId, auctionId, request, cancellationToken)))
            .WithName("ReverseAuctionSale")
            .WithSummary("Reverse a completed sale with an audit reason and purse reconciliation")
            .Produces<AuctionStateResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static RouteHandlerBuilder MapControl(
        RouteGroupBuilder group,
        string route,
        string name,
        string summary,
        Func<IAuctionService, Guid, Guid, CancellationToken, Task<AuctionStateResponse?>> execute) =>
        group.MapPost(route, async Task<Results<
                Ok<AuctionStateResponse>,
                NotFound,
                Conflict<ProblemDetails>,
                ForbidHttpResult,
                UnauthorizedHttpResult>> (
                Guid auctionId,
                ClaimsPrincipal principal,
                IAuctionService service,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(principal, userId => execute(service, userId, auctionId, cancellationToken)))
            .WithName(name)
            .WithSummary(summary)
            .Produces<AuctionStateResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

    private static async Task<Results<
        Ok<AuctionStateResponse>,
        NotFound,
        Conflict<ProblemDetails>,
        ForbidHttpResult,
        UnauthorizedHttpResult>> ExecuteAsync(
        ClaimsPrincipal principal,
        Func<Guid, Task<AuctionStateResponse?>> execute)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return TypedResults.Unauthorized();
        }
        try
        {
            var state = await execute(userId);
            return state is null ? TypedResults.NotFound() : TypedResults.Ok(state);
        }
        catch (AuctionPermissionException)
        {
            return TypedResults.Forbid();
        }
        catch (AuctionCommandRejectedException exception)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Auction action rejected",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
