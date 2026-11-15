using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using ProCricketAuction.Api.DTOs;
using ProCricketAuction.Api.Services;

namespace ProCricketAuction.Api.Endpoints;

public static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var tournaments = endpoints.MapGroup("/api/v1/tournaments").RequireAuthorization();

        tournaments.MapPost("/import", async Task<Results<
                Created<TournamentWorkspaceResponse>,
                Conflict<ProblemDetails>,
                BadRequest<ProblemDetails>,
                UnauthorizedHttpResult>> (
                ImportWorkspaceRequest request,
                HttpContext context,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetOwnerId(context.User, out var ownerId))
                {
                    return TypedResults.Unauthorized();
                }

                try
                {
                    var workspace = await service.ImportWorkspaceAsync(ownerId, request, cancellationToken);
                    return TypedResults.Created($"/api/v1/tournaments/{workspace.Tournament.Id}", workspace);
                }
                catch (WorkspaceAlreadyExistsException)
                {
                    return TypedResults.Conflict(new ProblemDetails
                    {
                        Title = "Workspace already exists",
                        Detail = "The account already has saved tournaments. Reload them instead of importing a second copy.",
                        Status = StatusCodes.Status409Conflict
                    });
                }
                catch (InvalidWorkspaceImportException)
                {
                    return TypedResults.BadRequest(new ProblemDetails
                    {
                        Title = "Invalid workspace import",
                        Detail = "The legacy workspace contains duplicate identifiers, invalid purse values, or inconsistent player sale details.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }
            })
            .WithName("ImportOrganizerWorkspace")
            .WithSummary("Import a legacy browser workspace into the organizer database in one transaction")
            .Produces<TournamentWorkspaceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        tournaments.MapGet("/", async (
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            await service.GetWorkspacesAsync(GetOwnerId(principal), cancellationToken))
            .WithName("GetOrganizerTournaments")
            .WithSummary("List the authenticated organizer's tournaments and their teams and players")
            .Produces<IReadOnlyList<TournamentWorkspaceResponse>>();

        tournaments.MapPost("/", async Task<Results<Created<TournamentResponse>, UnauthorizedHttpResult>> (
                TournamentRequest request,
                HttpContext context,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetOwnerId(context.User, out var ownerId))
                {
                    return TypedResults.Unauthorized();
                }

                var tournament = await service.CreateTournamentAsync(ownerId, request, cancellationToken);
                return TypedResults.Created($"/api/v1/tournaments/{tournament.Id}", tournament);
            })
            .WithName("CreateTournament")
            .WithSummary("Create a tournament owned by the authenticated organizer")
            .Produces<TournamentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        tournaments.MapPut("/{tournamentId:guid}", async Task<Results<Ok<TournamentResponse>, NotFound>> (
                Guid tournamentId,
                TournamentRequest request,
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                var tournament = await service.UpdateTournamentAsync(
                    GetOwnerId(principal), tournamentId, request, cancellationToken);
                return tournament is null ? TypedResults.NotFound() : TypedResults.Ok(tournament);
            })
            .WithName("UpdateTournament")
            .WithSummary("Update an organizer-owned tournament")
            .Produces<TournamentResponse>()
            .Produces(StatusCodes.Status404NotFound);

        tournaments.MapPost("/{tournamentId:guid}/seasons/{seasonId:guid}/copy-roster", async Task<Results<
                Ok<CopyRosterResponse>,
                NotFound,
                Conflict<ProblemDetails>,
                BadRequest<ProblemDetails>>> (
                Guid tournamentId,
                Guid seasonId,
                CopyRosterRequest request,
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var result = await service.CopyRosterAsync(
                        GetOwnerId(principal), tournamentId, seasonId, request, cancellationToken);
                    return result is null
                        ? TypedResults.NotFound()
                        : TypedResults.Ok(result);
                }
                catch (InvalidRosterCopyException)
                {
                    return TypedResults.BadRequest(new ProblemDetails
                    {
                        Title = "Invalid roster selection",
                        Detail = "Choose existing teams and players from a season you can manage.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }
                catch (DuplicateCopiedTeamException)
                {
                    return TypedResults.Conflict(new ProblemDetails
                    {
                        Title = "Team already exists in this season",
                        Detail = "The selected roster contains a team name or short code already registered in the destination season.",
                        Status = StatusCodes.Status409Conflict
                    });
                }
                catch (RosterCopyLimitExceededException)
                {
                    return TypedResults.Conflict(new ProblemDetails
                    {
                        Title = "Season roster limit exceeded",
                        Detail = "The selected teams or players exceed the configured limit for this season.",
                        Status = StatusCodes.Status409Conflict
                    });
                }
            })
            .WithName("CopySeasonRoster")
            .WithSummary("Copy selected teams and players into a season with fresh auction values")
            .Produces<CopyRosterResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        tournaments.MapDelete("/{tournamentId:guid}", async Task<Results<NoContent, NotFound, Conflict<ProblemDetails>>> (
                Guid tournamentId,
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                var deleted = await service.DeleteTournamentAsync(
                    GetOwnerId(principal), tournamentId, cancellationToken);
                if (deleted is null)
                {
                    return TypedResults.NotFound();
                }
                if (!deleted.Value)
                {
                    return TypedResults.Conflict(new ProblemDetails
                    {
                        Title = "Tournament has an active auction",
                        Detail = "Resolve and complete all live or paused auction events before deleting this tournament.",
                        Status = StatusCodes.Status409Conflict
                    });
                }
                return TypedResults.NoContent();
            })
            .WithName("DeleteTournament")
            .WithSummary("Delete an organizer-owned tournament and its related teams and players")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        tournaments.MapPost("/{tournamentId:guid}/seasons", async Task<Results<
                Created<SeasonResponse>,
                NotFound,
                Conflict<ProblemDetails>,
                BadRequest<ProblemDetails>>> (
                Guid tournamentId,
                SeasonRequest request,
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var season = await service.CreateSeasonAsync(
                        GetOwnerId(principal), tournamentId, request, cancellationToken);
                    return season is null
                        ? TypedResults.NotFound()
                        : TypedResults.Created($"/api/v1/tournaments/{tournamentId}/seasons/{season.Id}", season);
                }
                catch (DuplicateSeasonNameException)
                {
                    return TypedResults.Conflict(new ProblemDetails
                    {
                        Title = "Season name already exists",
                        Detail = "Choose a unique season name within this league.",
                        Status = StatusCodes.Status409Conflict
                    });
                }
                catch (InvalidSeasonDateRangeException)
                {
                    return TypedResults.BadRequest(new ProblemDetails
                    {
                        Title = "Invalid season dates",
                        Detail = "The season end date must be on or after its start date.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }
            })
            .WithName("CreateTournamentSeason")
            .WithSummary("Create a season inside an organizer-owned league")
            .Produces<SeasonResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        tournaments.MapPut("/{tournamentId:guid}/seasons/{seasonId:guid}", async Task<Results<
                Ok<SeasonResponse>,
                NotFound,
                Conflict<ProblemDetails>,
                BadRequest<ProblemDetails>>> (
                Guid tournamentId,
                Guid seasonId,
                SeasonRequest request,
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var season = await service.UpdateSeasonAsync(
                        GetOwnerId(principal), tournamentId, seasonId, request, cancellationToken);
                    return season is null ? TypedResults.NotFound() : TypedResults.Ok(season);
                }
                catch (DuplicateSeasonNameException)
                {
                    return TypedResults.Conflict(new ProblemDetails
                    {
                        Title = "Season name already exists",
                        Detail = "Choose a unique season name within this league.",
                        Status = StatusCodes.Status409Conflict
                    });
                }
                catch (InvalidSeasonDateRangeException)
                {
                    return TypedResults.BadRequest(new ProblemDetails
                    {
                        Title = "Invalid season dates",
                        Detail = "The season end date must be on or after its start date.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }
            })
            .WithName("UpdateTournamentSeason")
            .WithSummary("Update a season inside an organizer-owned league")
            .Produces<SeasonResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        tournaments.MapDelete("/{tournamentId:guid}/seasons/{seasonId:guid}", async Task<Results<
                NoContent,
                NotFound,
                Conflict<ProblemDetails>>> (
                Guid tournamentId,
                Guid seasonId,
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                var deleted = await service.DeleteSeasonAsync(
                    GetOwnerId(principal), tournamentId, seasonId, cancellationToken);
                if (deleted is null)
                {
                    return TypedResults.NotFound();
                }
                return deleted.Value
                    ? TypedResults.NoContent()
                    : TypedResults.Conflict(new ProblemDetails
                    {
                        Title = "Season cannot be deleted",
                        Detail = "Keep at least one season. Active seasons and seasons with teams, players, or auction events cannot be deleted.",
                        Status = StatusCodes.Status409Conflict
                    });
            })
            .WithName("DeleteTournamentSeason")
            .WithSummary("Delete an empty, inactive season without removing its parent league")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        tournaments.MapPost("/{tournamentId:guid}/auction-events", async Task<Results<
                Created<AuctionEventResponse>,
                NotFound,
                BadRequest<ProblemDetails>,
                Conflict<ProblemDetails>>> (
                Guid tournamentId,
                AuctionEventRequest request,
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var auctionEvent = await service.CreateAuctionEventAsync(
                        GetOwnerId(principal), tournamentId, request, cancellationToken);
                    return auctionEvent is null
                        ? TypedResults.NotFound()
                        : TypedResults.Created(
                            $"/api/v1/tournaments/{tournamentId}/auction-events/{auctionEvent.Id}",
                            auctionEvent);
                }
                catch (InvalidAuctionEventException)
                {
                    return TypedResults.BadRequest(new ProblemDetails
                    {
                        Title = "Invalid auction date",
                        Detail = "Choose a valid date and time for this auction event.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }
                catch (InvalidAuctionConfigurationException)
                {
                    return TypedResults.BadRequest(new ProblemDetails
                    {
                        Title = "Invalid auction rules",
                        Detail = "The minimum squad size cannot exceed the maximum squad size.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }
            })
            .WithName("CreateAuctionEvent")
            .WithSummary("Schedule an auction event for a season")
            .Produces<AuctionEventResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        tournaments.MapPut("/{tournamentId:guid}/auction-events/{eventId:guid}", async Task<Results<
                Ok<AuctionEventResponse>,
                NotFound,
                BadRequest<ProblemDetails>,
                Conflict<ProblemDetails>>> (
                Guid tournamentId,
                Guid eventId,
                AuctionEventRequest request,
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var auctionEvent = await service.UpdateAuctionEventAsync(
                        GetOwnerId(principal), tournamentId, eventId, request, cancellationToken);
                    return auctionEvent is null ? TypedResults.NotFound() : TypedResults.Ok(auctionEvent);
                }
                catch (InvalidAuctionEventException)
                {
                    return TypedResults.BadRequest(new ProblemDetails
                    {
                        Title = "Invalid auction date",
                        Detail = "Choose a valid date and time for this auction event.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }
                catch (InvalidAuctionConfigurationException)
                {
                    return TypedResults.BadRequest(new ProblemDetails
                    {
                        Title = "Invalid auction rules",
                        Detail = "The minimum squad size cannot exceed the maximum squad size.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }
                catch (AuctionEventCannotBeEditedException)
                {
                    return TypedResults.Conflict(new ProblemDetails
                    {
                        Title = "Auction event cannot be edited",
                        Detail = "Auction rules cannot change after bidding starts or the auction is completed.",
                        Status = StatusCodes.Status409Conflict
                    });
                }
            })
            .WithName("UpdateAuctionEvent")
            .WithSummary("Update a scheduled auction event")
            .Produces<AuctionEventResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        tournaments.MapDelete("/{tournamentId:guid}/auction-events/{eventId:guid}", async Task<Results<
                NoContent,
                NotFound,
                Conflict<ProblemDetails>>> (
                Guid tournamentId,
                Guid eventId,
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                var deleted = await service.DeleteAuctionEventAsync(
                    GetOwnerId(principal), tournamentId, eventId, cancellationToken);
                if (deleted is null)
                {
                    return TypedResults.NotFound();
                }
                return deleted.Value
                    ? TypedResults.NoContent()
                    : TypedResults.Conflict(new ProblemDetails
                    {
                        Title = "Auction event is live",
                        Detail = "A live auction event cannot be deleted.",
                        Status = StatusCodes.Status409Conflict
                    });
            })
            .WithName("DeleteAuctionEvent")
            .WithSummary("Delete an auction event that is not live")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        tournaments.MapGet("/{tournamentId:guid}/members", async Task<Results<Ok<IReadOnlyList<LeagueMemberResponse>>, NotFound>> (
                Guid tournamentId,
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                var members = await service.GetLeagueMembersAsync(
                    GetOwnerId(principal), tournamentId, cancellationToken);
                return members is null ? TypedResults.NotFound() : TypedResults.Ok(members);
            })
            .WithName("GetLeagueMembers")
            .WithSummary("List the owner and registered accounts assigned to a league")
            .Produces<IReadOnlyList<LeagueMemberResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        tournaments.MapPost("/{tournamentId:guid}/members", async Task<Results<
                Created<LeagueMemberResponse>,
                NotFound<ProblemDetails>,
                Conflict<ProblemDetails>,
                BadRequest<ProblemDetails>>> (
                Guid tournamentId,
                AddLeagueMemberRequest request,
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var member = await service.AddLeagueMemberAsync(
                        GetOwnerId(principal), tournamentId, request, cancellationToken);
                    return member is null
                        ? TypedResults.NotFound(new ProblemDetails { Title = "League not found or access denied." })
                        : TypedResults.Created(
                            $"/api/v1/tournaments/{tournamentId}/members/{member.UserId}",
                            member);
                }
                catch (UnregisteredLeagueMemberException)
                {
                    return TypedResults.NotFound(new ProblemDetails
                    {
                        Title = "Account not registered",
                        Detail = "Only an account that has already registered can be added. No invitation email is sent.",
                        Status = StatusCodes.Status404NotFound
                    });
                }
                catch (DuplicateLeagueMemberException)
                {
                    return TypedResults.Conflict(new ProblemDetails
                    {
                        Title = "Member already belongs to this league",
                        Detail = "Choose another registered account or update the existing member's role.",
                        Status = StatusCodes.Status409Conflict
                    });
                }
                catch (InvalidLeagueTeamAssignmentException)
                {
                    return TypedResults.BadRequest(new ProblemDetails
                    {
                        Title = "Invalid captain team assignment",
                        Detail = "Assigned teams must belong to this tournament, and each team can only be listed once.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }
            })
            .WithName("AddLeagueMember")
            .WithSummary("Assign an existing registered account a league role")
            .Produces<LeagueMemberResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        tournaments.MapPut("/{tournamentId:guid}/members/{userId:guid}", async Task<Results<
                Ok<LeagueMemberResponse>,
                NotFound,
                BadRequest<ProblemDetails>>> (
                Guid tournamentId,
                Guid userId,
                UpdateLeagueMemberRoleRequest request,
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var member = await service.UpdateLeagueMemberRoleAsync(
                        GetOwnerId(principal), tournamentId, userId, request, cancellationToken);
                    return member is null ? TypedResults.NotFound() : TypedResults.Ok(member);
                }
                catch (InvalidLeagueTeamAssignmentException)
                {
                    return TypedResults.BadRequest(new ProblemDetails
                    {
                        Title = "Invalid captain team assignment",
                        Detail = "Assigned teams must belong to this tournament, and each team can only be listed once.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }
            })
            .WithName("UpdateLeagueMemberRole")
            .WithSummary("Change an assigned league member's role")
            .Produces<LeagueMemberResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        tournaments.MapDelete("/{tournamentId:guid}/members/{userId:guid}", async Task<Results<NoContent, NotFound>> (
                Guid tournamentId,
                Guid userId,
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                var removed = await service.RemoveLeagueMemberAsync(
                    GetOwnerId(principal), tournamentId, userId, cancellationToken);
                return removed is null ? TypedResults.NotFound() : TypedResults.NoContent();
            })
            .WithName("RemoveLeagueMember")
            .WithSummary("Remove a registered account from the league")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        tournaments.MapPost("/{tournamentId:guid}/teams", CreateTeamAsync)
            .WithName("CreateTournamentTeam")
            .WithSummary("Add a team to an organizer-owned tournament")
            .Produces<TeamResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        tournaments.MapDelete("/{tournamentId:guid}/teams/{teamId:guid}", async Task<Results<NoContent, NotFound, Conflict<ProblemDetails>>> (
                Guid tournamentId,
                Guid teamId,
                ClaimsPrincipal principal,
                IWorkspaceService service,
                CancellationToken cancellationToken) =>
            {
                var deleted = await service.DeleteTeamAsync(
                    GetOwnerId(principal), tournamentId, teamId, cancellationToken);
                if (deleted is null)
                {
                    return TypedResults.NotFound();
                }
                if (!deleted.Value)
                {
                    return TypedResults.Conflict(new ProblemDetails
                    {
                        Title = "Team has an active auction",
                        Detail = "A team cannot be deleted while its season has a live, paused, or awaiting-decision auction. Complete or cancel the auction first.",
                        Status = StatusCodes.Status409Conflict
                    });
                }
                return TypedResults.NoContent();
            })
            .WithName("DeleteTournamentTeam")
            .WithSummary("Delete a team, return its players to the pool, and remove its auction history")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        tournaments.MapPost("/{tournamentId:guid}/players", CreatePlayerAsync)
            .WithName("CreateTournamentPlayer")
            .WithSummary("Register a player or add a purchased player to an organizer-owned team")
            .Produces<PlayerResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> CreateTeamAsync(
        Guid tournamentId,
        CreateTeamRequest request,
        HttpContext context,
        IWorkspaceService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetOwnerId(context.User, out var ownerId))
        {
            return TypedResults.Unauthorized();
        }

        try
        {
            var team = await service.CreateTeamAsync(ownerId, tournamentId, request, cancellationToken);
            return team is null
                ? TypedResults.NotFound()
                : TypedResults.Created($"/api/v1/tournaments/{tournamentId}/teams/{team.Id}", team);
        }
        catch (DuplicateTeamCodeException)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Team name already exists",
                Detail = "Choose a unique team name for this tournament.",
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (TeamLimitExceededException)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Team limit reached",
                Detail = "The league's configured team limit has been reached for this season.",
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    private static async Task<IResult> CreatePlayerAsync(
        Guid tournamentId,
        CreatePlayerRequest request,
        HttpContext context,
        IWorkspaceService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetOwnerId(context.User, out var ownerId))
        {
            return TypedResults.Unauthorized();
        }

        try
        {
            var player = await service.CreatePlayerAsync(ownerId, tournamentId, request, cancellationToken);
            return player is null
                ? TypedResults.NotFound()
                : TypedResults.Created($"/api/v1/tournaments/{tournamentId}/players/{player.Id}", player);
        }
        catch (InvalidPlayerStateException)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid player state",
                Detail = "Only a sold player can include both a winning team and a sale price.",
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InsufficientTeamPurseException)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Insufficient team purse",
                Detail = "The team's remaining purse is lower than this player's purchase price.",
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (PlayerLimitExceededException)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Player limit reached",
                Detail = "The league's configured player limit has been reached for this season.",
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    private static Guid GetOwnerId(ClaimsPrincipal principal)
    {
        if (!TryGetOwnerId(principal, out var ownerId))
        {
            throw new UnauthorizedAccessException("The signed-in account could not be identified.");
        }
        return ownerId;
    }

    private static bool TryGetOwnerId(ClaimsPrincipal principal, out Guid ownerId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out ownerId);
}
