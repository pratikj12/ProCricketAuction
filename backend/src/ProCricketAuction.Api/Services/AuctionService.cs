using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using ProCricketAuction.Api.DTOs;
using ProCricketAuction.Domain.Entities;
using ProCricketAuction.Infrastructure.Data;

namespace ProCricketAuction.Api.Services;

public sealed class AuctionService(
    ApplicationDbContext db,
    IAuctionNotifier notifier,
    ILogger<AuctionService> logger) : IAuctionService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> AuctionGates = new();
    private const long MaximumVirtualValue = 9_000_000_000_000_000;

    public async Task<AuctionStateResponse?> GetStateAsync(
        Guid userId,
        Guid auctionId,
        CancellationToken cancellationToken)
    {
        var auction = await db.Auctions.AsNoTracking()
            .Include(item => item.Tournament)
            .Include(item => item.Season)
            .SingleOrDefaultAsync(item => item.Id == auctionId, cancellationToken);
        if (auction is null || !await CanAccessAsync(userId, auction.TournamentId, cancellationToken))
        {
            return null;
        }

        return await BuildStateAsync(userId, auction, cancellationToken);
    }

    public async Task<AuctionStateResponse?> GetPublicStateAsync(
        Guid auctionId,
        CancellationToken cancellationToken)
    {
        var auction = await db.Auctions.AsNoTracking()
            .Include(item => item.Tournament)
            .Include(item => item.Season)
            .SingleOrDefaultAsync(item => item.Id == auctionId, cancellationToken);
        if (auction is null || auction.Status is "Scheduled" or "Cancelled")
        {
            return null;
        }

        var state = await BuildStateAsync(Guid.Empty, auction, cancellationToken);
        return state.ToPublicView();
    }

    public Task<AuctionStateResponse?> StartAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken) =>
        MutateAsync(userId, auctionId, true, async auction =>
        {
            if (auction.Status != "Scheduled")
            {
                throw new AuctionCommandRejectedException("Only a scheduled auction can be started.");
            }
            auction.Status = "Live";
            RecordAudit(auction, userId, "AuctionStarted", null, null, null, "Auction opened for bidding.");
            await Task.CompletedTask;
        }, cancellationToken);

    public Task<AuctionStateResponse?> PauseAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken) =>
        MutateAsync(userId, auctionId, true, async auction =>
        {
            if (auction.Status != "Live")
            {
                throw new AuctionCommandRejectedException("Only a live auction can be paused.");
            }
            auction.PausedRemainingMilliseconds = auction.BidDeadline.HasValue
                ? (int)Math.Clamp((auction.BidDeadline.Value - DateTimeOffset.UtcNow).TotalMilliseconds, 0, int.MaxValue)
                : null;
            auction.BidDeadline = null;
            auction.Status = "Paused";
            RecordAudit(auction, userId, "AuctionPaused", auction.CurrentPlayerId, auction.LeadingTeamId,
                auction.CurrentBidAmount, "Auction paused by an authorized operator.");
            await Task.CompletedTask;
        }, cancellationToken);

    public Task<AuctionStateResponse?> ResumeAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken) =>
        MutateAsync(userId, auctionId, true, async auction =>
        {
            if (auction.Status != "Paused")
            {
                throw new AuctionCommandRejectedException("Only a paused auction can be resumed.");
            }
            auction.Status = "Live";
            auction.BidDeadline = auction.CurrentPlayerId.HasValue
                ? DateTimeOffset.UtcNow.AddMilliseconds(auction.PausedRemainingMilliseconds ?? auction.BidDurationSeconds * 1000)
                : null;
            auction.PausedRemainingMilliseconds = null;
            RecordAudit(auction, userId, "AuctionResumed", auction.CurrentPlayerId, auction.LeadingTeamId,
                auction.CurrentBidAmount, "Auction resumed by an authorized operator.");
            await Task.CompletedTask;
        }, cancellationToken);

    public Task<AuctionStateResponse?> PresentPlayerAsync(
        Guid userId,
        Guid auctionId,
        PresentAuctionPlayerRequest request,
        CancellationToken cancellationToken) =>
        MutateAsync(userId, auctionId, true, async auction =>
        {
            if (auction.Status != "Live" || auction.CurrentPlayerId.HasValue)
            {
                throw new AuctionCommandRejectedException("Present a player only when the auction is live and no player is already on the block.");
            }
            var player = await db.Players.SingleOrDefaultAsync(
                item => item.Id == request.PlayerId && item.TournamentId == auction.TournamentId
                    && item.SeasonId == auction.SeasonId,
                cancellationToken);
            if (player is null || player.Status != "Available")
            {
                throw new AuctionCommandRejectedException("That player is not available in this auction season.");
            }
            Present(auction, player);
            RecordAudit(auction, userId, "PlayerPresented", player.Id, null, player.BasePrice,
                $"Player {player.FullName} presented for round {auction.CurrentRound}.");
        }, cancellationToken);

    public Task<AuctionStateResponse?> NextPlayerAsync(
        Guid userId,
        Guid auctionId,
        CancellationToken cancellationToken) =>
        MutateAsync(userId, auctionId, true, async auction =>
        {
            if (auction.Status != "Live" || auction.CurrentPlayerId.HasValue)
            {
                throw new AuctionCommandRejectedException("Finalize or skip the current player before advancing.");
            }
            await PresentNextAvailableAsync(auction, userId, cancellationToken);
        }, cancellationToken);

    public Task<AuctionStateResponse?> SkipPlayerAsync(
        Guid userId,
        Guid auctionId,
        CancellationToken cancellationToken) =>
        MutateAsync(userId, auctionId, true, async auction =>
        {
            var player = await GetActivePlayerAsync(auction, cancellationToken);
            if (player is null || auction.Status is not ("Live" or "AwaitingDecision"))
            {
                throw new AuctionCommandRejectedException("There is no active player to skip.");
            }
            auction.Status = "Live";
            player.Status = "Unsold";
            player.TeamId = null;
            player.SoldPrice = null;
            ClearCurrentPlayer(auction);
            RecordAudit(auction, userId, "PlayerSkipped", player.Id, null, null,
                "Player skipped by the auctioneer and marked unsold.");
            await PresentNextAvailableAsync(auction, userId, cancellationToken);
        }, cancellationToken);

    public Task<AuctionStateResponse?> MarkSoldAsync(
        Guid userId,
        Guid auctionId,
        ConfirmAuctionSaleRequest request,
        CancellationToken cancellationToken) =>
        MutateAsync(userId, auctionId, true, async auction =>
        {
            if (auction.Revision != request.ExpectedRevision)
            {
                throw new AuctionCommandRejectedException("The auction changed after the sale was reviewed. Refresh and confirm the latest accepted bid.");
            }
            var player = await GetActivePlayerAsync(auction, cancellationToken);
            if (player is null || auction.Status is not ("Live" or "AwaitingDecision")
                || auction.LeadingTeamId is null || auction.CurrentBidAmount < player.BasePrice)
            {
                throw new AuctionCommandRejectedException("A valid highest bid is required before a player can be sold.");
            }
            var team = await db.Teams.SingleOrDefaultAsync(
                item => item.Id == auction.LeadingTeamId && item.TournamentId == auction.TournamentId
                    && item.SeasonId == auction.SeasonId,
                cancellationToken);
            if (team is null || team.Purse < auction.CurrentBidAmount)
            {
                throw new AuctionCommandRejectedException("The winning team no longer has enough available purse.");
            }
            var squadCount = await db.Players.CountAsync(
                item => item.SeasonId == auction.SeasonId && item.TeamId == team.Id && item.Status == "Sold",
                cancellationToken);
            if (squadCount >= auction.MaximumSquadSize)
            {
                throw new AuctionCommandRejectedException("The winning team has reached its maximum squad size.");
            }
            if (await db.Purchases.AnyAsync(
                    purchase => purchase.PlayerId == player.Id && !purchase.IsReversed,
                    cancellationToken))
            {
                throw new AuctionCommandRejectedException("This player already has a confirmed purchase.");
            }

            var price = auction.CurrentBidAmount;
            team.Purse -= price;
            player.Status = "Sold";
            player.TeamId = team.Id;
            player.SoldPrice = price;
            db.Purchases.Add(new Purchase
            {
                Id = Guid.NewGuid(),
                AuctionId = auction.Id,
                PlayerId = player.Id,
                TeamId = team.Id,
                Price = price,
                Round = auction.CurrentRound,
                PurchasedAt = DateTimeOffset.UtcNow
            });
            auction.Status = "Live";
            ClearCurrentPlayer(auction);
            RecordAudit(auction, userId, "PlayerSold", player.Id, team.Id, price,
                $"Sale confirmed to {team.Name} for {price}.");
        }, cancellationToken);

    public Task<AuctionStateResponse?> MarkUnsoldAsync(
        Guid userId,
        Guid auctionId,
        CancellationToken cancellationToken) =>
        MutateAsync(userId, auctionId, true, async auction =>
        {
            var player = await GetActivePlayerAsync(auction, cancellationToken);
            if (player is null || auction.Status is not ("Live" or "AwaitingDecision"))
            {
                throw new AuctionCommandRejectedException("There is no active player to mark unsold.");
            }
            auction.Status = "Live";
            player.Status = "Unsold";
            player.TeamId = null;
            player.SoldPrice = null;
            ClearCurrentPlayer(auction);
            RecordAudit(auction, userId, "PlayerUnsold", player.Id, null, null,
                $"Player {player.FullName} marked unsold in round {auction.CurrentRound}.");
        }, cancellationToken);

    public Task<AuctionStateResponse?> StartNextRoundAsync(
        Guid userId,
        Guid auctionId,
        CancellationToken cancellationToken) =>
        MutateAsync(userId, auctionId, true, async auction =>
        {
            if (auction.Status is not ("Completed" or "Live")
                || auction.CurrentPlayerId.HasValue
                || await db.Players.AnyAsync(
                    player => player.SeasonId == auction.SeasonId && player.Status == "Available",
                    cancellationToken))
            {
                throw new AuctionCommandRejectedException("A later round can start only after the current round has no active or available players.");
            }
            if (auction.CurrentRound >= auction.MaximumRounds)
            {
                throw new AuctionCommandRejectedException("The configured maximum number of auction rounds has been reached.");
            }
            var unsold = await db.Players.Where(player =>
                    player.SeasonId == auction.SeasonId && player.Status == "Unsold")
                .ToListAsync(cancellationToken);
            if (unsold.Count == 0)
            {
                throw new AuctionCommandRejectedException("There are no unsold players available for another round.");
            }
            auction.CurrentRound++;
            auction.Status = "Live";
            foreach (var player in unsold)
            {
                player.Status = "Available";
            }
            RecordAudit(auction, userId, "AuctionRoundStarted", null, null, null,
                $"Round {auction.CurrentRound} opened with {unsold.Count} unsold players.");
            var firstPlayer = unsold.OrderBy(player => player.FullName).First();
            Present(auction, firstPlayer);
            RecordAudit(auction, userId, "PlayerPresented", firstPlayer.Id, null, firstPlayer.BasePrice,
                $"Player {firstPlayer.FullName} presented for round {auction.CurrentRound}.");
        }, cancellationToken);

    public async Task<AuctionStateResponse?> PlaceBidAsync(
        Guid userId,
        Guid auctionId,
        PlaceAuctionBidRequest request,
        CancellationToken cancellationToken)
    {
        var receivedAt = DateTimeOffset.UtcNow;
        var gate = AuctionGates.GetOrAdd(auctionId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var auction = await db.Auctions.Include(item => item.Tournament).SingleOrDefaultAsync(
                item => item.Id == auctionId, cancellationToken);
            if (auction is null || !await CanAccessAsync(userId, auction.TournamentId, cancellationToken))
            {
                return null;
            }
            var isCaptain = await db.LeagueMembers.AnyAsync(
                member => member.TournamentId == auction.TournamentId && member.UserId == userId
                    && member.Role == "Captain",
                cancellationToken);
            if (!isCaptain)
            {
                throw new AuctionPermissionException("Only an assigned captain can place a bid.");
            }

            var duplicate = await db.Bids.AsNoTracking().SingleOrDefaultAsync(
                bid => bid.AuctionId == auctionId && bid.ClientRequestId == request.ClientRequestId,
                cancellationToken);
            if (duplicate is not null)
            {
                if (duplicate.UserId != userId || duplicate.TeamId != request.TeamId || duplicate.Amount != request.Amount)
                {
                    throw new AuctionCommandRejectedException("This idempotency key was already used for a different bid.");
                }
                await transaction.CommitAsync(cancellationToken);
                if (!duplicate.Accepted)
                {
                    throw new AuctionBidRejectedException(
                        duplicate.RejectionReason ?? "This bid request was previously rejected.");
                }
                return await BuildStateAsync(userId, auction, cancellationToken);
            }

            var assignedTeam = await db.TeamMembers.AnyAsync(
                member => member.UserId == userId && member.TeamId == request.TeamId
                    && db.Teams.Any(team => team.Id == member.TeamId
                        && team.TournamentId == auction.TournamentId && team.SeasonId == auction.SeasonId),
                cancellationToken);
            if (!assignedTeam)
            {
                throw new AuctionPermissionException("You may bid only for your assigned team in this auction season.");
            }

            var player = auction.CurrentPlayerId.HasValue
                ? await db.Players.SingleOrDefaultAsync(
                    item => item.Id == auction.CurrentPlayerId.Value && item.TournamentId == auction.TournamentId
                        && item.SeasonId == auction.SeasonId,
                    cancellationToken)
                : null;
            var team = await db.Teams.SingleOrDefaultAsync(
                item => item.Id == request.TeamId && item.TournamentId == auction.TournamentId
                    && item.SeasonId == auction.SeasonId,
                cancellationToken);
            var nextBid = player is null ? 0 : auction.CurrentBidAmount == 0
                ? player.BasePrice
                : SafeAdd(auction.CurrentBidAmount, auction.BidIncrement);
            string? rejection = null;
            if (auction.Status is not ("Live" or "AwaitingDecision"))
            {
                rejection = "The auction is not accepting bids.";
            }
            else if (player is null || player.Status != "Available")
            {
                rejection = "There is no eligible player currently on the block.";
            }
            else if (auction.BidDeadline is null || receivedAt > auction.BidDeadline.Value)
            {
                auction.Status = "AwaitingDecision";
                rejection = "The server-controlled bidding deadline has passed.";
            }
            else if (request.ExpectedRevision != auction.Revision)
            {
                rejection = "The auction changed after this screen loaded. Refresh the current state and submit again.";
            }
            else if (team is null)
            {
                throw new AuctionPermissionException("The assigned team is not part of this auction season.");
            }
            else if (team.Id == auction.LeadingTeamId)
            {
                rejection = "Your team is already leading this player.";
            }
            else if (request.Amount < nextBid)
            {
                rejection = $"The next valid bid is {nextBid}.";
            }
            else if (request.Amount > MaximumVirtualValue || team.Purse < request.Amount)
            {
                rejection = "The bid exceeds the team's remaining purse.";
            }
            else if (await db.Players.CountAsync(
                         item => item.SeasonId == auction.SeasonId && item.TeamId == team.Id
                             && item.Status == "Sold",
                         cancellationToken) >= auction.MaximumSquadSize)
            {
                rejection = "The team has reached its maximum squad size.";
            }

            if (rejection is not null)
            {
                db.Bids.Add(new Bid
                {
                    Id = Guid.NewGuid(),
                    AuctionId = auctionId,
                    PlayerId = player?.Id,
                    TeamId = request.TeamId,
                    UserId = userId,
                    ClientRequestId = request.ClientRequestId,
                    Amount = request.Amount,
                    Accepted = false,
                    RejectionReason = rejection,
                    CreatedAt = receivedAt
                });
                RecordAudit(auction, userId, "BidRejected", player?.Id, request.TeamId, request.Amount, rejection);
                auction.Revision++;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                await PublishAsync(auctionId, await BuildStateAsync(Guid.Empty, auction, cancellationToken), cancellationToken);
                throw new AuctionBidRejectedException(rejection);
            }

            var bid = new Bid
            {
                Id = Guid.NewGuid(),
                AuctionId = auctionId,
                PlayerId = player!.Id,
                TeamId = request.TeamId,
                UserId = userId,
                ClientRequestId = request.ClientRequestId,
                Amount = request.Amount,
                SequenceNumber = ++auction.BidSequence,
                Accepted = true,
                CreatedAt = receivedAt
            };
            db.Bids.Add(bid);
            auction.CurrentBidAmount = request.Amount;
            auction.LeadingTeamId = team!.Id;
            auction.Status = "Live";
            var remaining = auction.BidDeadline!.Value - receivedAt;
            if (auction.BidExtensionSeconds > 0 && remaining.TotalSeconds <= auction.BidExtensionSeconds)
            {
                auction.BidDeadline = DateTimeOffset.UtcNow.AddSeconds(auction.BidExtensionSeconds);
            }
            RecordAudit(auction, userId, "BidAccepted", player.Id, team.Id, request.Amount,
                $"Accepted bid #{bid.SequenceNumber} for {team.Name}.");
            auction.Revision++;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var state = await BuildStateAsync(userId, auction, cancellationToken);
            await PublishAsync(auctionId, await BuildStateAsync(Guid.Empty, auction, cancellationToken), cancellationToken);
            return state;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AuctionCommandRejectedException("Another auction action was committed first. Refresh the latest state and retry.");
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<AuctionStateResponse?> ReverseSaleAsync(
        Guid userId,
        Guid auctionId,
        ReverseAuctionSaleRequest request,
        CancellationToken cancellationToken) =>
        MutateAsync(userId, auctionId, true, async auction =>
        {
            if (auction.Revision != request.ExpectedRevision)
            {
                throw new AuctionCommandRejectedException("The auction changed before the correction was confirmed. Refresh and review the sale again.");
            }
            if (!await CanCorrectAsync(userId, auction.TournamentId, cancellationToken))
            {
                throw new AuctionPermissionException("Only the tournament owner or an administrator can reverse a sale.");
            }
            var purchase = await db.Purchases.SingleOrDefaultAsync(
                item => item.Id == request.PurchaseId && item.AuctionId == auctionId && !item.IsReversed,
                cancellationToken);
            if (purchase is null)
            {
                throw new AuctionCommandRejectedException("The active purchase was not found or was already reversed.");
            }
            var player = await db.Players.SingleAsync(item => item.Id == purchase.PlayerId, cancellationToken);
            var team = await db.Teams.SingleAsync(item => item.Id == purchase.TeamId, cancellationToken);
            if (player.Status != "Sold" || player.TeamId != team.Id
                || team.Purse > team.InitialPurse - purchase.Price)
            {
                throw new AuctionCommandRejectedException("The sale no longer matches the persisted player and team purse records.");
            }

            team.Purse += purchase.Price;
            player.Status = "Available";
            player.TeamId = null;
            player.SoldPrice = null;
            purchase.IsReversed = true;
            purchase.ReversedAt = DateTimeOffset.UtcNow;
            purchase.CorrectionReason = request.Reason.Trim();
            auction.Status = "Paused";
            auction.CurrentPlayerId = player.Id;
            auction.CurrentBidAmount = 0;
            auction.LeadingTeamId = null;
            auction.BidDeadline = null;
            auction.PausedRemainingMilliseconds = 0;
            RecordAudit(auction, userId, "SaleReversed", player.Id, team.Id, purchase.Price,
                $"Purchase {purchase.Id} reversed: {request.Reason.Trim()}");
        }, cancellationToken);

    public async Task ExpireDueAuctionsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var candidates = await db.Auctions.AsNoTracking()
            .Where(auction => auction.Status == "Live" && auction.CurrentPlayerId.HasValue
                && auction.BidDeadline.HasValue)
            .Select(auction => new { auction.Id, auction.BidDeadline })
            .ToListAsync(cancellationToken);
        var dueAuctions = candidates
            .Where(auction => auction.BidDeadline <= now)
            .Select(auction => auction.Id);
        foreach (var auctionId in dueAuctions)
        {
            await TryExpireAsync(auctionId, cancellationToken);
        }
    }

    private async Task TryExpireAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        var gate = AuctionGates.GetOrAdd(auctionId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var auction = await db.Auctions.Include(item => item.Tournament)
                .SingleOrDefaultAsync(item => item.Id == auctionId, cancellationToken);
            if (auction is null || auction.Status != "Live" || auction.CurrentPlayerId is null
                || auction.BidDeadline is null || auction.BidDeadline > DateTimeOffset.UtcNow)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            auction.Status = "AwaitingDecision";
            auction.Revision++;
            RecordAudit(auction, null, "BidDeadlineReached", auction.CurrentPlayerId, auction.LeadingTeamId,
                auction.CurrentBidAmount == 0 ? null : auction.CurrentBidAmount,
                auction.LeadingTeamId.HasValue
                    ? "Bidding deadline reached; operator confirmation is required."
                    : "Bidding deadline reached with no bids; operator must mark the player unsold or extend play.");
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var state = await BuildStateAsync(Guid.Empty, auction, cancellationToken);
            await PublishAsync(auctionId, state, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation("Auction {AuctionId} deadline was resolved by a concurrent command.", auctionId);
            return;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<AuctionStateResponse?> MutateAsync(
        Guid userId,
        Guid auctionId,
        bool requireOperator,
        Func<Auction, Task> mutation,
        CancellationToken cancellationToken)
    {
        var gate = AuctionGates.GetOrAdd(auctionId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var auction = await db.Auctions.Include(item => item.Tournament).Include(item => item.Season)
                .SingleOrDefaultAsync(item => item.Id == auctionId, cancellationToken);
            if (auction is null || !await CanAccessAsync(userId, auction.TournamentId, cancellationToken))
            {
                return null;
            }
            if (requireOperator && !await CanOperateAsync(userId, auction.TournamentId, cancellationToken))
            {
                throw new AuctionPermissionException("Only the tournament owner, an administrator, or an auctioneer can control this auction.");
            }
            await mutation(auction);
            auction.Revision++;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var state = await BuildStateAsync(userId, auction, cancellationToken);
            await PublishAsync(auctionId, await BuildStateAsync(Guid.Empty, auction, cancellationToken), cancellationToken);
            return state;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AuctionCommandRejectedException("Another auction action was committed first. Refresh the latest state and retry.");
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<AuctionStateResponse> BuildStateAsync(
        Guid userId,
        Auction auction,
        CancellationToken cancellationToken)
    {
        var tournament = auction.Tournament ?? await db.Tournaments.AsNoTracking()
            .SingleAsync(item => item.Id == auction.TournamentId, cancellationToken);
        var season = auction.Season ?? await db.TournamentSeasons.AsNoTracking()
            .SingleAsync(item => item.Id == auction.SeasonId, cancellationToken);
        var teams = await db.Teams.AsNoTracking()
            .Where(team => team.TournamentId == auction.TournamentId && team.SeasonId == auction.SeasonId)
            .OrderBy(team => team.Name)
            .ToListAsync(cancellationToken);
        var teamIds = teams.Select(team => team.Id).ToArray();
        var assignedTeamIds = userId == Guid.Empty ? [] : await db.TeamMembers.AsNoTracking()
            .Where(member => member.UserId == userId && teamIds.Contains(member.TeamId))
            .Select(member => member.TeamId)
            .ToListAsync(cancellationToken);
        var players = await db.Players.AsNoTracking()
            .Where(player => player.TournamentId == auction.TournamentId && player.SeasonId == auction.SeasonId)
            .OrderBy(player => player.FullName)
            .ToListAsync(cancellationToken);
        var soldCounts = players.Where(player => player.Status == "Sold" && player.TeamId.HasValue)
            .GroupBy(player => player.TeamId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var teamResponses = teams.Select(team =>
        {
            var purchased = soldCounts.GetValueOrDefault(team.Id);
            var reserveSlots = Math.Max(0, auction.MinimumSquadSize - purchased - 1);
            var reserve = (long)reserveSlots * auction.MinimumSquadReservePrice;
            var affordable = Math.Max(0, team.Purse - reserve);
            var isAssigned = assignedTeamIds.Contains(team.Id);
            var next = auction.CurrentPlayerId.HasValue
                ? players.FirstOrDefault(player => player.Id == auction.CurrentPlayerId.Value) is { } current
                    ? auction.CurrentBidAmount == 0 ? current.BasePrice : SafeAdd(auction.CurrentBidAmount, auction.BidIncrement)
                    : 0
                : 0;
            var canBid = isAssigned && auction.Status == "Live" && auction.BidDeadline.HasValue
                && auction.BidDeadline.Value > DateTimeOffset.UtcNow
                && auction.CurrentPlayerId.HasValue && team.Id != auction.LeadingTeamId
                && team.Purse >= next && purchased < auction.MaximumSquadSize;
            return new AuctionTeamStateResponse(
                team.Id, team.Name, team.ShortName, team.DisplayName, team.LogoDataUrl, team.Color,
                team.InitialPurse, Math.Max(0, team.InitialPurse - team.Purse), team.Purse,
                purchased, Math.Max(0, auction.MaximumSquadSize - purchased), affordable, canBid);
        }).ToArray();

        var currentPlayer = players.FirstOrDefault(player => player.Id == auction.CurrentPlayerId);
        var recentBids = await db.Bids.AsNoTracking()
            .Where(bid => bid.AuctionId == auction.Id && bid.Accepted
                && (!auction.CurrentPlayerId.HasValue || bid.PlayerId == auction.CurrentPlayerId.Value))
            .OrderByDescending(bid => bid.SequenceNumber)
            .Take(8)
            .Join(db.Teams.AsNoTracking(), bid => bid.TeamId, team => team.Id,
                (bid, team) => new AuctionBidResponse(
                    bid.Id, bid.PlayerId ?? Guid.Empty, team.Id, team.Name, team.ShortName, bid.Amount,
                    bid.SequenceNumber, bid.CreatedAt))
            .ToListAsync(cancellationToken);
        var recentAuctionBids = await db.Bids.AsNoTracking()
            .Where(bid => bid.AuctionId == auction.Id && bid.Accepted)
            .OrderByDescending(bid => bid.SequenceNumber)
            .Take(8)
            .Join(db.Teams.AsNoTracking(), bid => bid.TeamId, team => team.Id,
                (bid, team) => new AuctionBidResponse(
                    bid.Id, bid.PlayerId ?? Guid.Empty, team.Id, team.Name, team.ShortName, bid.Amount,
                    bid.SequenceNumber, bid.CreatedAt))
            .ToListAsync(cancellationToken);
        var recentSales = (await db.Purchases.AsNoTracking()
            .Where(purchase => purchase.AuctionId == auction.Id)
            .Join(db.Players.AsNoTracking(), purchase => purchase.PlayerId, player => player.Id,
                (purchase, player) => new { purchase, player })
            .Join(db.Teams.AsNoTracking(), item => item.purchase.TeamId, team => team.Id,
                (item, team) => new AuctionPurchaseResponse(
                    item.purchase.Id, item.player.Id, item.player.FullName, team.Id, team.Name,
                    item.purchase.Price, item.purchase.Round, item.purchase.PurchasedAt, item.purchase.IsReversed))
            .ToListAsync(cancellationToken))
            .OrderByDescending(purchase => purchase.PurchasedAt)
            .Take(12)
            .ToArray();
        var auditEvents = await db.AuditLogs.AsNoTracking()
            .Where(audit => audit.AuctionId == auction.Id)
            .OrderByDescending(audit => audit.EventSequence)
            .Take(20)
            .Select(audit => new AuctionAuditEventResponse(
                audit.EventSequence, audit.EventType, audit.Details, audit.PlayerId,
                audit.TeamId, audit.Amount, audit.CreatedAt))
            .ToListAsync(cancellationToken);
        var bidCount = currentPlayer is null ? 0 : await db.Bids.CountAsync(
            bid => bid.AuctionId == auction.Id && bid.PlayerId == currentPlayer.Id && bid.Accepted,
            cancellationToken);
        var leadingTeam = teams.FirstOrDefault(team => team.Id == auction.LeadingTeamId);
        var nextBidAmount = currentPlayer is null ? 0
            : auction.CurrentBidAmount == 0 ? currentPlayer.BasePrice
            : SafeAdd(auction.CurrentBidAmount, auction.BidIncrement);

        return new AuctionStateResponse(
            auction.Id,
            auction.TournamentId,
            auction.SeasonId,
            tournament.Name,
            season.Name,
            auction.Name,
            tournament.CurrencyCode,
            auction.Status,
            currentPlayer is null ? null : ToPlayerResponse(currentPlayer),
            auction.CurrentBidAmount,
            nextBidAmount,
            auction.LeadingTeamId,
            leadingTeam?.Name,
            bidCount,
            auction.BidDeadline,
            auction.PausedRemainingMilliseconds,
            auction.BidDurationSeconds,
            auction.BidExtensionSeconds,
            auction.BidIncrement,
            auction.CurrentRound,
            auction.MaximumRounds,
            auction.BidSequence,
            auction.EventSequence,
            auction.Revision,
            assignedTeamIds.Count > 0 ? assignedTeamIds[0] : null,
            assignedTeamIds,
            teamResponses,
            players.Select(ToPlayerResponse).ToArray(),
            recentBids,
            recentAuctionBids,
            recentSales,
            auditEvents);
    }

    private async Task<bool> CanAccessAsync(Guid userId, Guid tournamentId, CancellationToken cancellationToken) =>
        await db.Tournaments.AnyAsync(
            tournament => tournament.Id == tournamentId && tournament.OwnerId == userId,
            cancellationToken)
        || await db.LeagueMembers.AnyAsync(
            member => member.TournamentId == tournamentId && member.UserId == userId,
            cancellationToken);

    private async Task<bool> CanOperateAsync(Guid userId, Guid tournamentId, CancellationToken cancellationToken) =>
        await db.Tournaments.AnyAsync(
            tournament => tournament.Id == tournamentId && tournament.OwnerId == userId,
            cancellationToken)
        || await db.LeagueMembers.AnyAsync(
            member => member.TournamentId == tournamentId && member.UserId == userId
                && (member.Role == "Administrator" || member.Role == "Auctioneer"),
            cancellationToken);

    private async Task<bool> CanCorrectAsync(Guid userId, Guid tournamentId, CancellationToken cancellationToken) =>
        await db.Tournaments.AnyAsync(
            tournament => tournament.Id == tournamentId && tournament.OwnerId == userId,
            cancellationToken)
        || await db.LeagueMembers.AnyAsync(
            member => member.TournamentId == tournamentId && member.UserId == userId
                && member.Role == "Administrator",
            cancellationToken);

    private async Task<Player?> GetActivePlayerAsync(Auction auction, CancellationToken cancellationToken) =>
        auction.CurrentPlayerId.HasValue
            ? await db.Players.SingleOrDefaultAsync(
                player => player.Id == auction.CurrentPlayerId.Value
                    && player.TournamentId == auction.TournamentId && player.SeasonId == auction.SeasonId,
                cancellationToken)
            : null;

    private async Task PresentNextAvailableAsync(
        Auction auction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var player = await db.Players
            .Where(item => item.TournamentId == auction.TournamentId && item.SeasonId == auction.SeasonId
                && item.Status == "Available")
            .OrderBy(item => item.FullName)
            .FirstOrDefaultAsync(cancellationToken);
        if (player is null)
        {
            auction.Status = "Completed";
            auction.BidDeadline = null;
            RecordAudit(auction, userId, "AuctionCompleted", null, null, null,
                $"Round {auction.CurrentRound} completed; no available players remain.");
            return;
        }
        Present(auction, player);
        RecordAudit(auction, userId, "PlayerPresented", player.Id, null, player.BasePrice,
            $"Player {player.FullName} presented for round {auction.CurrentRound}.");
    }

    private static void Present(Auction auction, Player player)
    {
        auction.CurrentPlayerId = player.Id;
        auction.CurrentBidAmount = 0;
        auction.LeadingTeamId = null;
        auction.BidDeadline = DateTimeOffset.UtcNow.AddSeconds(auction.BidDurationSeconds);
        auction.PausedRemainingMilliseconds = null;
    }

    private static void ClearCurrentPlayer(Auction auction)
    {
        auction.CurrentPlayerId = null;
        auction.CurrentBidAmount = 0;
        auction.LeadingTeamId = null;
        auction.BidDeadline = null;
        auction.PausedRemainingMilliseconds = null;
    }

    private void RecordAudit(
        Auction auction,
        Guid? actorUserId,
        string eventType,
        Guid? playerId,
        Guid? teamId,
        long? amount,
        string details)
    {
        auction.EventSequence++;
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            AuctionId = auction.Id,
            ActorUserId = actorUserId,
            EventSequence = auction.EventSequence,
            EventType = eventType,
            PlayerId = playerId,
            TeamId = teamId,
            Amount = amount,
            Details = details,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private Task PublishAsync(Guid auctionId, AuctionStateResponse state, CancellationToken cancellationToken) =>
        notifier.PublishAsync(auctionId, state, cancellationToken);

    private static AuctionPlayerResponse ToPlayerResponse(Player player) =>
        new(player.Id, $"P-{player.Id.ToString("N")[..6].ToUpperInvariant()}",
            player.FullName, player.Role, player.City, player.PhotoDataUrl,
            player.BasePrice, player.Status, player.TeamId, player.SoldPrice);

    private static long SafeAdd(long first, long second) =>
        first > MaximumVirtualValue - second ? MaximumVirtualValue : first + second;
}

public class AuctionCommandRejectedException(string message) : Exception(message);
public sealed class AuctionBidRejectedException(string message) : AuctionCommandRejectedException(message);
public sealed class AuctionPermissionException(string message) : Exception(message);
