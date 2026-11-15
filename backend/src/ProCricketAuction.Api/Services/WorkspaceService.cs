using Microsoft.EntityFrameworkCore;
using ProCricketAuction.Api.DTOs;
using ProCricketAuction.Domain.Entities;
using ProCricketAuction.Infrastructure.Data;

namespace ProCricketAuction.Api.Services;

public sealed class WorkspaceService(ApplicationDbContext db) : IWorkspaceService
{
    public async Task<IReadOnlyList<TournamentWorkspaceResponse>> GetWorkspacesAsync(
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var tournaments = await db.Set<Tournament>()
            .AsNoTracking()
            .Where(tournament => tournament.OwnerId == ownerId
                || db.LeagueMembers.Any(member => member.TournamentId == tournament.Id && member.UserId == ownerId))
            .ToListAsync(cancellationToken);
        tournaments.Sort((left, right) => right.CreatedAt.CompareTo(left.CreatedAt));

        if (tournaments.Count == 0)
        {
            return [];
        }

        var tournamentIds = tournaments.Select(tournament => tournament.Id).ToArray();
        var seasons = await db.TournamentSeasons.AsNoTracking()
            .Where(season => tournamentIds.Contains(season.TournamentId))
            .ToListAsync(cancellationToken);
        seasons.Sort((left, right) => left.CreatedAt.CompareTo(right.CreatedAt));
        var teams = await db.Set<Team>().AsNoTracking()
            .Where(team => tournamentIds.Contains(team.TournamentId))
            .OrderBy(team => team.Name)
            .ToListAsync(cancellationToken);
        var players = await db.Set<Player>().AsNoTracking()
            .Where(player => tournamentIds.Contains(player.TournamentId))
            .OrderBy(player => player.FullName)
            .ToListAsync(cancellationToken);
        var auctionEvents = await db.Auctions.AsNoTracking()
            .Where(auction => tournamentIds.Contains(auction.TournamentId))
            .ToListAsync(cancellationToken);
        auctionEvents.Sort((left, right) => left.ScheduledAt.CompareTo(right.ScheduledAt));

        return tournaments.Select(tournament => new TournamentWorkspaceResponse(
            ToResponse(tournament),
            seasons.Where(season => season.TournamentId == tournament.Id)
                .Select(season => ToResponse(
                    season,
                    teams.Count(team => team.SeasonId == season.Id),
                    players.Count(player => player.SeasonId == season.Id),
                    players.Count(player => player.SeasonId == season.Id && player.Status == "Sold"),
                    players.Count(player => player.SeasonId == season.Id && player.Status == "Available"),
                    players.Where(player => player.SeasonId == season.Id && player.Status == "Sold")
                        .Sum(player => player.SoldPrice ?? 0)))
                .ToArray(),
            teams.Where(team => team.TournamentId == tournament.Id).Select(ToResponse).ToArray(),
            players.Where(player => player.TournamentId == tournament.Id).Select(ToResponse).ToArray(),
            auctionEvents.Where(auction => auction.TournamentId == tournament.Id).Select(ToResponse).ToArray()
        )).ToArray();
    }

    public async Task<TournamentWorkspaceResponse> ImportWorkspaceAsync(
        Guid ownerId,
        ImportWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Teams.Count > 100 || request.Players.Count > 5000)
        {
            throw new InvalidWorkspaceImportException();
        }
        if (await db.Set<Tournament>().AnyAsync(tournament => tournament.OwnerId == ownerId, cancellationToken))
        {
            throw new WorkspaceAlreadyExistsException();
        }

        var duplicateLegacyTeamIds = request.Teams
            .GroupBy(team => team.LegacyId, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);
        var duplicateShortNames = request.Teams
            .GroupBy(team => team.ShortName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);
        var duplicateLegacyPlayerIds = request.Players
            .GroupBy(player => player.LegacyId, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);
        var invalidPurse = request.Teams.Any(team => team.Purse > team.InitialPurse);
        if (duplicateLegacyTeamIds || duplicateShortNames || duplicateLegacyPlayerIds || invalidPurse)
        {
            throw new InvalidWorkspaceImportException();
        }

        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = request.Tournament.Name.Trim(),
            Season = request.Tournament.Season.Trim(),
            Venue = request.Tournament.Venue.Trim(),
            DefaultTeamPurse = request.Tournament.DefaultTeamPurse,
            CurrencyCode = request.Tournament.CurrencyCode,
            MaximumTeams = request.Tournament.MaximumTeams,
            MaximumPlayers = request.Tournament.MaximumPlayers,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var season = CreateInitialSeason(tournament, request.Tournament.Season);
        var teamIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var teams = request.Teams.Select(source =>
        {
            var teamId = Guid.NewGuid();
            teamIds.Add(source.LegacyId, teamId);
            return new Team
            {
                Id = teamId,
                TournamentId = tournament.Id,
                SeasonId = season.Id,
                Name = source.Name.Trim(),
                ShortName = source.ShortName.Trim().ToUpperInvariant(),
                DisplayName = source.DisplayName.Trim(),
                Captain = source.Captain.Trim(),
                City = source.City.Trim(),
                LogoDataUrl = source.LogoDataUrl,
                Purse = source.Purse,
                InitialPurse = source.InitialPurse,
                Color = source.Color
            };
        }).ToArray();
        var players = request.Players.Select(source =>
        {
            var teamId = source.TeamLegacyId is not null && teamIds.TryGetValue(source.TeamLegacyId, out var mappedTeamId)
                ? mappedTeamId
                : (Guid?)null;
            if (source.Status == "Sold" && (!teamId.HasValue || !source.SoldPrice.HasValue)
                || source.Status != "Sold" && (source.TeamLegacyId is not null || source.SoldPrice.HasValue))
            {
                throw new InvalidWorkspaceImportException();
            }

            return new Player
            {
                Id = Guid.NewGuid(),
                TournamentId = tournament.Id,
                SeasonId = season.Id,
                FullName = source.Name.Trim(),
                City = source.City.Trim(),
                Email = source.Email.Trim(),
                Nationality = source.Nationality.Trim(),
                DateOfBirth = source.DateOfBirth,
                PhotoDataUrl = source.PhotoDataUrl,
                Role = source.Role,
                BasePrice = source.BasePrice,
                Status = source.Status,
                TeamId = teamId,
                SoldPrice = source.SoldPrice
            };
        }).ToArray();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Add(tournament);
        db.Add(season);
        db.AddRange(teams);
        db.AddRange(players);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new TournamentWorkspaceResponse(
            ToResponse(tournament),
            [ToResponse(season)],
            teams.Select(ToResponse).ToArray(),
            players.Select(ToResponse).ToArray(),
            []);
    }

    public async Task<TournamentResponse> CreateTournamentAsync(
        Guid ownerId,
        TournamentRequest request,
        CancellationToken cancellationToken)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = request.Name.Trim(),
            Season = request.Season.Trim(),
            Venue = request.Venue.Trim(),
            DefaultTeamPurse = request.DefaultTeamPurse,
            CurrencyCode = request.CurrencyCode,
            MaximumTeams = request.MaximumTeams,
            MaximumPlayers = request.MaximumPlayers,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var season = CreateInitialSeason(tournament, request.Season);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Add(tournament);
        db.Add(season);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(tournament);
    }

    public async Task<TournamentResponse?> UpdateTournamentAsync(
        Guid ownerId,
        Guid tournamentId,
        TournamentRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator"], cancellationToken))
        {
            return null;
        }
        var tournament = await FindTournamentAsync(ownerId, tournamentId, cancellationToken);
        if (tournament is null)
        {
            return null;
        }

        tournament.Name = request.Name.Trim();
        tournament.Season = request.Season.Trim();
        tournament.Venue = request.Venue.Trim();
        tournament.DefaultTeamPurse = request.DefaultTeamPurse;
        tournament.CurrencyCode = request.CurrencyCode;
        tournament.MaximumTeams = request.MaximumTeams;
        tournament.MaximumPlayers = request.MaximumPlayers;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(tournament);
    }

    public async Task<SeasonResponse?> CreateSeasonAsync(
        Guid ownerId,
        Guid tournamentId,
        SeasonRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator"], cancellationToken))
        {
            return null;
        }
        ValidateSeasonDates(request);
        if (await db.TournamentSeasons.AnyAsync(
                season => season.TournamentId == tournamentId
                    && season.Name.ToLower() == request.Name.Trim().ToLower(),
                cancellationToken))
        {
            throw new DuplicateSeasonNameException();
        }

        var season = new TournamentSeason
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Name = request.Name.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Venue = request.Venue.Trim(),
            Status = request.Status,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.TournamentSeasons.Add(season);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(season);
    }

    public async Task<CopyRosterResponse?> CopyRosterAsync(
        Guid ownerId,
        Guid tournamentId,
        Guid targetSeasonId,
        CopyRosterRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator"], cancellationToken)
            || !await CanManageLeagueAsync(ownerId, request.SourceTournamentId, ["Administrator"], cancellationToken))
        {
            return null;
        }

        if (request.TeamIds.Count > 100 || request.PlayerIds.Count > 5000
            || request.TeamIds.Distinct().Count() != request.TeamIds.Count
            || request.PlayerIds.Distinct().Count() != request.PlayerIds.Count)
        {
            throw new InvalidRosterCopyException();
        }

        var target = await FindTournamentAsync(ownerId, tournamentId, cancellationToken);
        var targetSeason = await FindSeasonAsync(ownerId, tournamentId, targetSeasonId, cancellationToken);
        var sourceSeason = await FindSeasonAsync(
            ownerId, request.SourceTournamentId, request.SourceSeasonId, cancellationToken);
        if (target is null || targetSeason is null || sourceSeason is null)
        {
            return null;
        }

        var sourceTeams = request.TeamIds.Count == 0
            ? []
            : await db.Teams.AsNoTracking()
                .Where(team => team.TournamentId == request.SourceTournamentId
                    && team.SeasonId == sourceSeason.Id && request.TeamIds.Contains(team.Id))
                .ToListAsync(cancellationToken);
        var sourcePlayers = request.PlayerIds.Count == 0
            ? []
            : await db.Players.AsNoTracking()
                .Where(player => player.TournamentId == request.SourceTournamentId
                    && player.SeasonId == sourceSeason.Id && request.PlayerIds.Contains(player.Id))
                .ToListAsync(cancellationToken);
        if (sourceTeams.Count != request.TeamIds.Count || sourcePlayers.Count != request.PlayerIds.Count)
        {
            throw new InvalidRosterCopyException();
        }

        var existingTeams = await db.Teams
            .Where(team => team.SeasonId == targetSeason.Id)
            .ToListAsync(cancellationToken);
        var existingPlayersCount = await db.Players.CountAsync(
            player => player.SeasonId == targetSeason.Id, cancellationToken);
        if (target.MaximumTeams.HasValue
                && existingTeams.Count + sourceTeams.Count > target.MaximumTeams.Value
            || target.MaximumPlayers.HasValue
                && existingPlayersCount + sourcePlayers.Count > target.MaximumPlayers.Value)
        {
            throw new RosterCopyLimitExceededException();
        }

        var existingNames = existingTeams.Select(team => team.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingShortNames = existingTeams.Select(team => team.ShortName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (sourceTeams.Any(team => !existingNames.Add(team.Name) || !existingShortNames.Add(team.ShortName)))
        {
            throw new DuplicateCopiedTeamException();
        }

        var usedColors = existingTeams.Select(team => team.Color).ToList();
        var copiedTeams = sourceTeams.Select(source =>
        {
            var color = CreateTeamColor(usedColors.Count, usedColors);
            usedColors.Add(color);
            return new Team
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                SeasonId = targetSeason.Id,
                Name = source.Name,
                ShortName = source.ShortName,
                DisplayName = source.DisplayName,
                Captain = source.Captain,
                City = source.City,
                LogoDataUrl = source.LogoDataUrl,
                Purse = target.DefaultTeamPurse,
                InitialPurse = target.DefaultTeamPurse,
                Color = color
            };
        }).ToArray();
        var copiedPlayers = sourcePlayers.Select(source => new Player
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            SeasonId = targetSeason.Id,
            FullName = source.FullName,
            City = source.City,
            Email = source.Email,
            Nationality = source.Nationality,
            DateOfBirth = source.DateOfBirth,
            PhotoDataUrl = source.PhotoDataUrl,
            Role = source.Role,
            BasePrice = source.BasePrice,
            Status = "Available",
            TeamId = null,
            SoldPrice = null
        }).ToArray();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Teams.AddRange(copiedTeams);
        db.Players.AddRange(copiedPlayers);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CopyRosterResponse(copiedTeams.Length, copiedPlayers.Length);
    }

    public async Task<SeasonResponse?> UpdateSeasonAsync(
        Guid ownerId,
        Guid tournamentId,
        Guid seasonId,
        SeasonRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator"], cancellationToken))
        {
            return null;
        }
        var season = await db.TournamentSeasons.SingleOrDefaultAsync(
            item => item.Id == seasonId && item.TournamentId == tournamentId
                && (item.Tournament!.OwnerId == ownerId
                    || db.LeagueMembers.Any(member => member.TournamentId == tournamentId && member.UserId == ownerId)),
            cancellationToken);
        if (season is null)
        {
            return null;
        }
        ValidateSeasonDates(request);
        if (await db.TournamentSeasons.AnyAsync(
                item => item.TournamentId == tournamentId && item.Id != seasonId
                    && item.Name.ToLower() == request.Name.Trim().ToLower(),
                cancellationToken))
        {
            throw new DuplicateSeasonNameException();
        }

        season.Name = request.Name.Trim();
        season.StartDate = request.StartDate;
        season.EndDate = request.EndDate;
        season.Venue = request.Venue.Trim();
        season.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(season);
    }

    public async Task<bool?> DeleteSeasonAsync(
        Guid ownerId,
        Guid tournamentId,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator"], cancellationToken))
        {
            return null;
        }
        var season = await db.TournamentSeasons.SingleOrDefaultAsync(
            item => item.Id == seasonId && item.TournamentId == tournamentId
                && (item.Tournament!.OwnerId == ownerId
                    || db.LeagueMembers.Any(member => member.TournamentId == tournamentId && member.UserId == ownerId)),
            cancellationToken);
        if (season is null)
        {
            return null;
        }
        var seasonCount = await db.TournamentSeasons.CountAsync(
            item => item.TournamentId == tournamentId,
            cancellationToken);
        var hasTeams = await db.Teams.AnyAsync(team => team.SeasonId == seasonId, cancellationToken);
        var hasPlayers = await db.Players.AnyAsync(player => player.SeasonId == seasonId, cancellationToken);
        var hasAuctionEvents = await db.Auctions.AnyAsync(auction => auction.SeasonId == seasonId, cancellationToken);
        if (seasonCount <= 1 || season.Status == "Active" || hasTeams || hasPlayers || hasAuctionEvents)
        {
            return false;
        }

        db.TournamentSeasons.Remove(season);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AuctionEventResponse?> CreateAuctionEventAsync(
        Guid ownerId,
        Guid tournamentId,
        AuctionEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator", "Auctioneer"], cancellationToken))
        {
            return null;
        }
        var season = await FindSeasonAsync(ownerId, tournamentId, request.SeasonId, cancellationToken);
        if (season is null)
        {
            return null;
        }
        if (request.ScheduledAt == default)
        {
            throw new InvalidAuctionEventException();
        }
        ValidateAuctionLifecycleStatus(request.Status);

        var auction = new Auction
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            SeasonId = season.Id,
            Name = request.Name.Trim(),
            ScheduledAt = request.ScheduledAt,
            Venue = request.Venue.Trim(),
            Description = request.Description.Trim(),
            Status = request.Status,
            BidIncrement = request.BidIncrement,
            BidDurationSeconds = request.BidDurationSeconds,
            BidExtensionSeconds = request.BidExtensionSeconds,
            MaximumSquadSize = request.MaximumSquadSize,
            MinimumSquadSize = request.MinimumSquadSize,
            MinimumSquadReservePrice = request.MinimumSquadReservePrice,
            MaximumRounds = request.MaximumRounds,
            CreatedAt = DateTimeOffset.UtcNow
        };
        ValidateAuctionConfiguration(request);
        db.Auctions.Add(auction);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(auction);
    }

    public async Task<AuctionEventResponse?> UpdateAuctionEventAsync(
        Guid ownerId,
        Guid tournamentId,
        Guid eventId,
        AuctionEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator", "Auctioneer"], cancellationToken))
        {
            return null;
        }
        var auction = await db.Auctions.SingleOrDefaultAsync(
            item => item.Id == eventId && item.TournamentId == tournamentId
                && (item.Tournament!.OwnerId == ownerId
                    || db.LeagueMembers.Any(member => member.TournamentId == tournamentId && member.UserId == ownerId)),
            cancellationToken);
        if (auction is null)
        {
            return null;
        }
        if (auction.Status is "Live" or "Paused" or "Completed")
        {
            throw new AuctionEventCannotBeEditedException();
        }
        var season = await FindSeasonAsync(ownerId, tournamentId, request.SeasonId, cancellationToken);
        if (season is null)
        {
            return null;
        }
        if (request.ScheduledAt == default)
        {
            throw new InvalidAuctionEventException();
        }
        ValidateAuctionLifecycleStatus(request.Status);
        ValidateAuctionConfiguration(request);

        auction.SeasonId = season.Id;
        auction.Name = request.Name.Trim();
        auction.ScheduledAt = request.ScheduledAt;
        auction.Venue = request.Venue.Trim();
        auction.Description = request.Description.Trim();
        auction.Status = request.Status;
        auction.BidIncrement = request.BidIncrement;
        auction.BidDurationSeconds = request.BidDurationSeconds;
        auction.BidExtensionSeconds = request.BidExtensionSeconds;
        auction.MaximumSquadSize = request.MaximumSquadSize;
        auction.MinimumSquadSize = request.MinimumSquadSize;
        auction.MinimumSquadReservePrice = request.MinimumSquadReservePrice;
        auction.MaximumRounds = request.MaximumRounds;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(auction);
    }

    public async Task<bool?> DeleteAuctionEventAsync(
        Guid ownerId,
        Guid tournamentId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator", "Auctioneer"], cancellationToken))
        {
            return null;
        }
        var auction = await db.Auctions.SingleOrDefaultAsync(
            item => item.Id == eventId && item.TournamentId == tournamentId
                && (item.Tournament!.OwnerId == ownerId
                    || db.LeagueMembers.Any(member => member.TournamentId == tournamentId && member.UserId == ownerId)),
            cancellationToken);
        if (auction is null)
        {
            return null;
        }
        if (auction.Status is "Live" or "Paused" or "AwaitingDecision" or "Completed"
            || await db.Bids.AnyAsync(bid => bid.AuctionId == eventId, cancellationToken)
            || await db.Purchases.AnyAsync(purchase => purchase.AuctionId == eventId, cancellationToken))
        {
            return false;
        }

        db.Auctions.Remove(auction);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TeamResponse?> CreateTeamAsync(
        Guid ownerId,
        Guid tournamentId,
        CreateTeamRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator", "Auctioneer"], cancellationToken))
        {
            return null;
        }
        var tournament = await FindTournamentAsync(ownerId, tournamentId, cancellationToken);
        if (tournament is null)
        {
            return null;
        }
        var season = await FindSeasonAsync(ownerId, tournamentId, request.SeasonId, cancellationToken);
        if (season is null)
        {
            return null;
        }

        var duplicateName = await db.Set<Team>().AnyAsync(
            team => team.SeasonId == season.Id
                && (team.Name.ToLower() == request.Name.Trim().ToLower()
                    || team.ShortName == request.ShortName.Trim().ToUpper()),
            cancellationToken);
        if (duplicateName)
        {
            throw new DuplicateTeamCodeException();
        }

        var existingTeams = await db.Set<Team>()
            .Where(team => team.SeasonId == season.Id)
            .ToListAsync(cancellationToken);
        if (tournament.MaximumTeams.HasValue && existingTeams.Count >= tournament.MaximumTeams.Value)
        {
            throw new TeamLimitExceededException();
        }
        var teamCount = existingTeams.Count;
        var team = new Team
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            SeasonId = season.Id,
            Name = request.Name.Trim(),
            ShortName = request.ShortName.Trim().ToUpperInvariant(),
            DisplayName = request.DisplayName.Trim(),
            Captain = request.Captain.Trim(),
            City = request.City.Trim(),
            LogoDataUrl = request.LogoDataUrl,
            Purse = tournament.DefaultTeamPurse,
            InitialPurse = tournament.DefaultTeamPurse,
            Color = CreateTeamColor(teamCount, existingTeams.Select(existing => existing.Color).ToArray())
        };
        db.Add(team);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(team);
    }

    public async Task<bool?> DeleteTeamAsync(
        Guid ownerId,
        Guid tournamentId,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator", "Auctioneer"], cancellationToken))
        {
            return null;
        }

        var team = await db.Set<Team>().SingleOrDefaultAsync(
            candidate => candidate.Id == teamId && candidate.TournamentId == tournamentId,
            cancellationToken);
        if (team is null)
        {
            return null;
        }
        if (await db.Auctions.AnyAsync(
                auction => auction.SeasonId == team.SeasonId
                    && (auction.Status == "Live"
                        || auction.Status == "Paused"
                        || auction.Status == "AwaitingDecision"),
                cancellationToken))
        {
            return false;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var assignedPlayers = await db.Players
            .Where(player => player.TeamId == teamId)
            .ToListAsync(cancellationToken);
        foreach (var player in assignedPlayers)
        {
            player.Status = "Available";
            player.TeamId = null;
            player.SoldPrice = null;
        }

        db.Bids.RemoveRange(await db.Bids
            .Where(bid => bid.TeamId == teamId)
            .ToListAsync(cancellationToken));
        db.Purchases.RemoveRange(await db.Purchases
            .Where(purchase => purchase.TeamId == teamId)
            .ToListAsync(cancellationToken));
        db.AuditLogs.RemoveRange(await db.AuditLogs
            .Where(audit => audit.TeamId == teamId)
            .ToListAsync(cancellationToken));
        db.Remove(team);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<PlayerResponse?> CreatePlayerAsync(
        Guid ownerId,
        Guid tournamentId,
        CreatePlayerRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator", "Auctioneer"], cancellationToken))
        {
            return null;
        }
        if (await FindTournamentAsync(ownerId, tournamentId, cancellationToken) is null)
        {
            return null;
        }
        var season = await FindSeasonAsync(ownerId, tournamentId, request.SeasonId, cancellationToken);
        if (season is null)
        {
            return null;
        }
        var tournament = await FindTournamentAsync(ownerId, tournamentId, cancellationToken);
        if (tournament is null)
        {
            return null;
        }
        if (tournament.MaximumPlayers.HasValue
            && await db.Players.CountAsync(player => player.SeasonId == season.Id, cancellationToken) >= tournament.MaximumPlayers.Value)
        {
            throw new PlayerLimitExceededException();
        }

        Team? team = null;
        if (request.TeamId.HasValue)
        {
            team = await db.Set<Team>().SingleOrDefaultAsync(
                candidate => candidate.Id == request.TeamId.Value
                && candidate.TournamentId == tournamentId
                && candidate.SeasonId == season.Id,
                cancellationToken);
            if (team is null)
            {
                return null;
            }
        }
        if (request.Status == "Sold" && (!request.TeamId.HasValue || !request.SoldPrice.HasValue))
        {
            throw new InvalidPlayerStateException();
        }
        if (request.Status != "Sold" && (request.TeamId.HasValue || request.SoldPrice.HasValue))
        {
            throw new InvalidPlayerStateException();
        }

        var player = new Player
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            SeasonId = season.Id,
            FullName = request.Name.Trim(),
            City = request.City.Trim(),
            Email = request.Email.Trim(),
            Nationality = request.Nationality.Trim(),
            DateOfBirth = request.DateOfBirth,
            PhotoDataUrl = request.PhotoDataUrl,
            Role = request.Role,
            BasePrice = request.BasePrice,
            Status = request.Status,
            TeamId = request.TeamId,
            SoldPrice = request.SoldPrice
        };
        if (request.Status == "Sold" && team is not null && request.SoldPrice.HasValue)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var charged = await db.Set<Team>()
                .Where(candidate => candidate.Id == team.Id
                    && candidate.TournamentId == tournamentId
                    && candidate.SeasonId == season.Id
                    && candidate.Purse >= request.SoldPrice.Value)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        candidate => candidate.Purse,
                        candidate => candidate.Purse - request.SoldPrice.Value),
                    cancellationToken);
            if (charged == 0)
            {
                throw new InsufficientTeamPurseException();
            }

            db.Add(player);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            db.Add(player);
            await db.SaveChangesAsync(cancellationToken);
        }
        return ToResponse(player);
    }

    public async Task<bool?> DeleteTournamentAsync(
        Guid ownerId,
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await FindOwnedTournamentAsync(ownerId, tournamentId, cancellationToken);
        if (tournament is null)
        {
            return null;
        }
        if (await db.Auctions.AnyAsync(
                auction => auction.TournamentId == tournamentId
                    && (auction.Status == "Live"
                        || auction.Status == "Paused"
                        || auction.Status == "AwaitingDecision"),
                cancellationToken))
        {
            return false;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Remove(tournament);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<LeagueMemberResponse>?> GetLeagueMembersAsync(
        Guid ownerId,
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator"], cancellationToken))
        {
            return null;
        }
        var tournament = await FindTournamentAsync(ownerId, tournamentId, cancellationToken);
        if (tournament is null)
        {
            return null;
        }
        var members = await db.LeagueMembers.AsNoTracking()
            .Include(member => member.User)
            .Where(member => member.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);
        var userIds = members.Select(member => member.UserId).ToArray();
        var assignments = await db.TeamMembers.AsNoTracking()
            .Where(member => userIds.Contains(member.UserId))
            .ToListAsync(cancellationToken);
        var responses = members
            .OrderBy(member => member.User!.Email)
            .Select(member => ToResponse(
                member,
                assignments.Where(assignment => assignment.UserId == member.UserId)
                    .Select(assignment => assignment.TeamId).ToArray()))
            .Prepend(new LeagueMemberResponse(
                tournament.OwnerId,
                tournament.Owner?.Email ?? string.Empty,
                tournament.Owner?.DisplayName ?? "League owner",
                "Owner",
                [],
                tournament.CreatedAt))
            .ToArray();
        return responses;
    }

    public async Task<LeagueMemberResponse?> AddLeagueMemberAsync(
        Guid ownerId,
        Guid tournamentId,
        AddLeagueMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator"], cancellationToken))
        {
            return null;
        }
        var tournament = await FindTournamentAsync(ownerId, tournamentId, cancellationToken);
        if (tournament is null)
        {
            return null;
        }
        var user = await db.Users.SingleOrDefaultAsync(
            account => account.Email != null
                && account.Email.ToLower() == request.Email.Trim().ToLower(),
            cancellationToken);
        if (user is null)
        {
            throw new UnregisteredLeagueMemberException();
        }
        if (user.Id == tournament.OwnerId)
        {
            throw new DuplicateLeagueMemberException();
        }
        await ValidateTeamAssignmentsAsync(tournamentId, request.TeamIds, cancellationToken);
        if (await db.LeagueMembers.AnyAsync(
                member => member.TournamentId == tournamentId && member.UserId == user.Id,
                cancellationToken))
        {
            throw new DuplicateLeagueMemberException();
        }

        var member = new LeagueMember
        {
            TournamentId = tournamentId,
            UserId = user.Id,
            Role = request.Role,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.LeagueMembers.Add(member);
        db.TeamMembers.AddRange(request.TeamIds.Select(teamId => new TeamMember
        {
            TeamId = teamId,
            UserId = user.Id
        }));
        await db.SaveChangesAsync(cancellationToken);
        member.User = user;
        return ToResponse(member, request.TeamIds);
    }

    public async Task<LeagueMemberResponse?> UpdateLeagueMemberRoleAsync(
        Guid ownerId,
        Guid tournamentId,
        Guid userId,
        UpdateLeagueMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator"], cancellationToken))
        {
            return null;
        }
        var member = await db.LeagueMembers
            .Include(item => item.User)
            .SingleOrDefaultAsync(
                item => item.TournamentId == tournamentId && item.UserId == userId,
                cancellationToken);
        if (member is null)
        {
            return null;
        }
        await ValidateTeamAssignmentsAsync(tournamentId, request.TeamIds, cancellationToken);
        member.Role = request.Role;
        var existingAssignments = await db.TeamMembers
            .Where(item => item.UserId == userId
                && db.Teams.Any(team => team.Id == item.TeamId && team.TournamentId == tournamentId))
            .ToListAsync(cancellationToken);
        db.TeamMembers.RemoveRange(existingAssignments);
        db.TeamMembers.AddRange(request.TeamIds.Select(teamId => new TeamMember
        {
            TeamId = teamId,
            UserId = userId
        }));
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(member, request.TeamIds);
    }

    public async Task<bool?> RemoveLeagueMemberAsync(
        Guid ownerId,
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await CanManageLeagueAsync(ownerId, tournamentId, ["Administrator"], cancellationToken))
        {
            return null;
        }
        var member = await db.LeagueMembers.SingleOrDefaultAsync(
            item => item.TournamentId == tournamentId && item.UserId == userId,
            cancellationToken);
        if (member is null)
        {
            return null;
        }
        var assignments = await db.TeamMembers
            .Where(item => item.UserId == userId
                && db.Teams.Any(team => team.Id == item.TeamId && team.TournamentId == tournamentId))
            .ToListAsync(cancellationToken);
        db.TeamMembers.RemoveRange(assignments);
        db.LeagueMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<Tournament?> FindTournamentAsync(Guid ownerId, Guid tournamentId, CancellationToken cancellationToken) =>
        db.Set<Tournament>().Include(tournament => tournament.Owner).SingleOrDefaultAsync(
            tournament => tournament.Id == tournamentId
                && (tournament.OwnerId == ownerId
                    || db.LeagueMembers.Any(member => member.TournamentId == tournamentId && member.UserId == ownerId)),
            cancellationToken);

    private Task<Tournament?> FindOwnedTournamentAsync(
        Guid ownerId,
        Guid tournamentId,
        CancellationToken cancellationToken) =>
        db.Set<Tournament>().SingleOrDefaultAsync(
            tournament => tournament.Id == tournamentId && tournament.OwnerId == ownerId,
            cancellationToken);

    private async Task<bool> CanManageLeagueAsync(
        Guid userId,
        Guid tournamentId,
        string[] allowedRoles,
        CancellationToken cancellationToken)
    {
        if (await db.Tournaments.AnyAsync(
                tournament => tournament.Id == tournamentId && tournament.OwnerId == userId,
                cancellationToken))
        {
            return true;
        }
        return await db.LeagueMembers.AnyAsync(
            member => member.TournamentId == tournamentId && member.UserId == userId
                && allowedRoles.Contains(member.Role),
            cancellationToken);
    }

    private async Task ValidateTeamAssignmentsAsync(
        Guid tournamentId,
        IReadOnlyList<Guid> teamIds,
        CancellationToken cancellationToken)
    {
        if (teamIds.Count > 100 || teamIds.Distinct().Count() != teamIds.Count)
        {
            throw new InvalidLeagueTeamAssignmentException();
        }
        if (teamIds.Count == 0)
        {
            return;
        }
        var matchingTeams = await db.Teams.CountAsync(
            team => team.TournamentId == tournamentId && teamIds.Contains(team.Id),
            cancellationToken);
        if (matchingTeams != teamIds.Count)
        {
            throw new InvalidLeagueTeamAssignmentException();
        }
    }

    private static LeagueMemberResponse ToResponse(LeagueMember member, IReadOnlyList<Guid> teamIds) =>
        new(member.UserId, member.User?.Email ?? string.Empty, member.User?.DisplayName ?? string.Empty,
            member.Role, teamIds, member.CreatedAt);

    private async Task<TournamentSeason?> FindSeasonAsync(
        Guid ownerId,
        Guid tournamentId,
        Guid? seasonId,
        CancellationToken cancellationToken)
    {
        var seasons = await db.TournamentSeasons
            .Where(season => season.TournamentId == tournamentId
                && (season.Tournament!.OwnerId == ownerId
                    || db.LeagueMembers.Any(member => member.TournamentId == tournamentId && member.UserId == ownerId)))
            .Where(season => !seasonId.HasValue || season.Id == seasonId.Value)
            .ToListAsync(cancellationToken);
        seasons.Sort((left, right) => left.CreatedAt.CompareTo(right.CreatedAt));
        return seasons.FirstOrDefault();
    }

    private static TournamentResponse ToResponse(Tournament tournament) =>
        new(tournament.Id, tournament.Name, tournament.Season, tournament.Venue,
            tournament.DefaultTeamPurse, tournament.CurrencyCode, tournament.MaximumTeams,
            tournament.MaximumPlayers, tournament.Status, tournament.CreatedAt);

    private static TeamResponse ToResponse(Team team) =>
        new(team.Id, team.TournamentId, team.SeasonId, team.Name, team.ShortName, team.DisplayName,
            team.Captain, team.City, team.LogoDataUrl, team.Purse, team.InitialPurse, team.Color);

    private static PlayerResponse ToResponse(Player player) =>
        new(player.Id, player.TournamentId, player.SeasonId, player.FullName, player.City, player.Email,
            player.Nationality, player.DateOfBirth, player.PhotoDataUrl, player.Role,
            player.BasePrice, player.Status, player.TeamId, player.SoldPrice);

    private static SeasonResponse ToResponse(
        TournamentSeason season,
        int teamCount = 0,
        int playerCount = 0,
        int soldPlayerCount = 0,
        int availablePlayerCount = 0,
        long totalSpend = 0) =>
        new(season.Id, season.TournamentId, season.Name, season.StartDate, season.EndDate,
            season.Venue, season.Status, teamCount, playerCount, soldPlayerCount,
            availablePlayerCount, totalSpend, season.CreatedAt);

    private static AuctionEventResponse ToResponse(Auction auction) =>
        new(auction.Id, auction.TournamentId, auction.SeasonId, auction.Name,
            auction.ScheduledAt, auction.Venue, auction.Description, auction.Status, auction.CreatedAt,
            auction.BidIncrement, auction.BidDurationSeconds, auction.BidExtensionSeconds,
            auction.MaximumSquadSize, auction.MinimumSquadSize, auction.MinimumSquadReservePrice,
            auction.MaximumRounds);

    private static TournamentSeason CreateInitialSeason(Tournament tournament, string requestedName) => new()
    {
        Id = Guid.NewGuid(),
        TournamentId = tournament.Id,
        Name = string.IsNullOrWhiteSpace(requestedName) ? "Season 1" : requestedName.Trim(),
        Venue = tournament.Venue,
        Status = "Upcoming",
        CreatedAt = tournament.CreatedAt
    };

    private static void ValidateSeasonDates(SeasonRequest request)
    {
        if (request.StartDate.HasValue && request.EndDate.HasValue
            && request.EndDate.Value < request.StartDate.Value)
        {
            throw new InvalidSeasonDateRangeException();
        }
    }

    private static void ValidateAuctionConfiguration(AuctionEventRequest request)
    {
        if (request.MinimumSquadSize > request.MaximumSquadSize)
        {
            throw new InvalidAuctionConfigurationException();
        }
    }

    private static void ValidateAuctionLifecycleStatus(string status)
    {
        if (status is not ("Scheduled" or "Cancelled"))
        {
            throw new InvalidAuctionEventException();
        }
    }

    private static string CreateTeamColor(int sequence, IReadOnlyCollection<string> usedColors)
    {
        var existingColors = usedColors.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var colorIndex = sequence;
        string color;
        do
        {
            color = GetTeamColor(colorIndex++);
        } while (existingColors.Contains(color));
        return color;
    }

    private static string GetTeamColor(int sequence)
    {
        var hue = (sequence * 137.508) % 360;
        const double saturation = 0.78;
        const double lightness = 0.58;
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        var match = lightness - chroma / 2;
        var (red, green, blue) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        return $"#{ToHex(red + match)}{ToHex(green + match)}{ToHex(blue + match)}";

        static string ToHex(double component) =>
            ((int)Math.Round(component * 255)).ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed class DuplicateTeamCodeException : Exception
{
}

public sealed class TeamLimitExceededException : Exception
{
}

public sealed class PlayerLimitExceededException : Exception
{
}

public sealed class InvalidPlayerStateException : Exception
{
}

public sealed class InsufficientTeamPurseException : Exception
{
}

public sealed class WorkspaceAlreadyExistsException : Exception
{
}

public sealed class InvalidWorkspaceImportException : Exception
{
}

public sealed class InvalidRosterCopyException : Exception
{
}

public sealed class DuplicateCopiedTeamException : Exception
{
}

public sealed class RosterCopyLimitExceededException : Exception
{
}

public sealed class DuplicateSeasonNameException : Exception
{
}

public sealed class InvalidSeasonDateRangeException : Exception
{
}

public sealed class InvalidAuctionEventException : Exception
{
}

public sealed class InvalidAuctionConfigurationException : Exception
{
}

public sealed class AuctionEventCannotBeEditedException : Exception
{
}

public sealed class InvalidLeagueTeamAssignmentException : Exception
{
}

public sealed class UnregisteredLeagueMemberException : Exception
{
}

public sealed class DuplicateLeagueMemberException : Exception
{
}
