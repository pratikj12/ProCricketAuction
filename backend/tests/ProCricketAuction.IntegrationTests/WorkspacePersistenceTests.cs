using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProCricketAuction.Api.DTOs;
using ProCricketAuction.Api.Services;
using ProCricketAuction.Domain.Entities;
using ProCricketAuction.Infrastructure.Data;
using Xunit;

namespace ProCricketAuction.IntegrationTests;

public sealed class WorkspacePersistenceTests
{
    [Fact]
    public async Task TournamentCannotBeDeletedWhileAnAuctionIsLiveOrAwaitingDecision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var ownerId = Guid.NewGuid();
        db.Users.Add(CreateUser(ownerId, "active-auction-delete@example.com"));
        await db.SaveChangesAsync();
        var service = new WorkspaceService(db);
        var tournament = await service.CreateTournamentAsync(ownerId, new TournamentRequest
        {
            Name = "Active Auction Cup",
            Season = "2026",
            DefaultTeamPurse = 100000
        }, CancellationToken.None);
        var season = Assert.Single((await service.GetWorkspacesAsync(ownerId, CancellationToken.None))
            .Single().Seasons);
        var auction = await service.CreateAuctionEventAsync(ownerId, tournament.Id, new AuctionEventRequest
        {
            SeasonId = season.Id,
            Name = "Live Event",
            ScheduledAt = DateTimeOffset.UtcNow
        }, CancellationToken.None);
        Assert.NotNull(auction);
        var persistedAuction = await db.Auctions.SingleAsync(item => item.Id == auction.Id);
        persistedAuction.Status = "AwaitingDecision";
        await db.SaveChangesAsync();

        Assert.False(await service.DeleteTournamentAsync(ownerId, tournament.Id, CancellationToken.None));
        Assert.True(await db.Tournaments.AnyAsync(item => item.Id == tournament.Id));
    }

    [Fact]
    public async Task DeletingTeamReturnsPlayersAndRemovesItsAuctionHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var ownerId = Guid.NewGuid();
        db.Users.Add(CreateUser(ownerId, "delete-team@example.com"));
        await db.SaveChangesAsync();
        var service = new WorkspaceService(db);
        var tournament = await service.CreateTournamentAsync(ownerId, new TournamentRequest
        {
            Name = "Team Deletion Cup",
            Season = "2026",
            DefaultTeamPurse = 100000
        }, CancellationToken.None);
        var season = Assert.Single((await service.GetWorkspacesAsync(ownerId, CancellationToken.None))
            .Single().Seasons);
        var team = await service.CreateTeamAsync(ownerId, tournament.Id, new CreateTeamRequest
        {
            Name = "Deleting Team",
            ShortName = "DEL",
            Captain = "Captain",
            SeasonId = season.Id
        }, CancellationToken.None);
        Assert.NotNull(team);
        var player = await service.CreatePlayerAsync(ownerId, tournament.Id, new CreatePlayerRequest
        {
            Name = "Assigned Player",
            Role = "Batter",
            BasePrice = 10000,
            Status = "Sold",
            TeamId = team.Id,
            SoldPrice = 20000,
            SeasonId = season.Id
        }, CancellationToken.None);
        Assert.NotNull(player);
        var auctionEvent = await service.CreateAuctionEventAsync(ownerId, tournament.Id, new AuctionEventRequest
        {
            SeasonId = season.Id,
            Name = "Completed Auction",
            ScheduledAt = DateTimeOffset.UtcNow
        }, CancellationToken.None);
        Assert.NotNull(auctionEvent);
        var auction = await db.Auctions.SingleAsync(item => item.Id == auctionEvent.Id);
        auction.Status = "Live";
        db.Bids.Add(new Bid
        {
            Id = Guid.NewGuid(),
            AuctionId = auction.Id,
            PlayerId = player.Id,
            TeamId = team.Id,
            UserId = ownerId,
            ClientRequestId = Guid.NewGuid(),
            Amount = 20000,
            SequenceNumber = 1,
            Accepted = true
        });
        db.Purchases.Add(new Purchase
        {
            Id = Guid.NewGuid(),
            AuctionId = auction.Id,
            PlayerId = player.Id,
            TeamId = team.Id,
            Price = 20000,
            Round = 1
        });
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            AuctionId = auction.Id,
            EventSequence = 1,
            EventType = "SaleConfirmed",
            PlayerId = player.Id,
            TeamId = team.Id
        });
        await db.SaveChangesAsync();

        Assert.False(await service.DeleteTeamAsync(ownerId, tournament.Id, team.Id, CancellationToken.None));
        auction.Status = "Completed";
        await db.SaveChangesAsync();
        Assert.True(await service.DeleteTeamAsync(ownerId, tournament.Id, team.Id, CancellationToken.None));

        var updatedPlayer = await db.Players.SingleAsync(item => item.Id == player.Id);
        Assert.Equal("Available", updatedPlayer.Status);
        Assert.Null(updatedPlayer.TeamId);
        Assert.Null(updatedPlayer.SoldPrice);
        Assert.False(await db.Teams.AnyAsync(item => item.Id == team.Id));
        Assert.False(await db.Bids.AnyAsync(item => item.TeamId == team.Id));
        Assert.False(await db.Purchases.AnyAsync(item => item.TeamId == team.Id));
        Assert.False(await db.AuditLogs.AnyAsync(item => item.TeamId == team.Id));
    }

    [Fact]
    public async Task SelectedRosterCopiesIntoNewSeasonWithFreshPursesAndAvailablePlayers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var ownerId = Guid.NewGuid();
        db.Users.Add(CreateUser(ownerId, "roster-copy@example.com"));
        await db.SaveChangesAsync();
        var service = new WorkspaceService(db);
        var tournament = await service.CreateTournamentAsync(ownerId, new TournamentRequest
        {
            Name = "Returning Teams Cup",
            Season = "2026",
            Venue = "Pune",
            DefaultTeamPurse = 200000
        }, CancellationToken.None);
        var sourceSeason = Assert.Single((await service.GetWorkspacesAsync(ownerId, CancellationToken.None))
            .Single().Seasons);
        var sourceTeam = await service.CreateTeamAsync(ownerId, tournament.Id, new CreateTeamRequest
        {
            Name = "Pune Panthers",
            ShortName = "PUN",
            Captain = "Captain",
            SeasonId = sourceSeason.Id
        }, CancellationToken.None);
        Assert.NotNull(sourceTeam);
        var sourcePlayer = await service.CreatePlayerAsync(ownerId, tournament.Id, new CreatePlayerRequest
        {
            Name = "Returning Player",
            Role = "Batter",
            BasePrice = 10000,
            Status = "Sold",
            TeamId = sourceTeam.Id,
            SoldPrice = 50000,
            SeasonId = sourceSeason.Id
        }, CancellationToken.None);
        Assert.NotNull(sourcePlayer);

        var targetSeason = await service.CreateSeasonAsync(ownerId, tournament.Id, new SeasonRequest
        {
            Name = "2027"
        }, CancellationToken.None);
        Assert.NotNull(targetSeason);
        var copied = await service.CopyRosterAsync(ownerId, tournament.Id, targetSeason.Id, new CopyRosterRequest
        {
            SourceTournamentId = tournament.Id,
            SourceSeasonId = sourceSeason.Id,
            TeamIds = [sourceTeam.Id],
            PlayerIds = [sourcePlayer.Id]
        }, CancellationToken.None);

        Assert.Equal(new CopyRosterResponse(1, 1), copied);
        var workspace = (await service.GetWorkspacesAsync(ownerId, CancellationToken.None)).Single();
        var copiedTeam = Assert.Single(workspace.Teams, team => team.SeasonId == targetSeason.Id);
        var copiedPlayer = Assert.Single(workspace.Players, player => player.SeasonId == targetSeason.Id);
        Assert.Equal(sourceTeam.Name, copiedTeam.Name);
        Assert.Equal("PUN", copiedTeam.ShortName);
        Assert.Equal(200000, copiedTeam.Purse);
        Assert.Equal(200000, copiedTeam.InitialPurse);
        Assert.Equal("Available", copiedPlayer.Status);
        Assert.Null(copiedPlayer.TeamId);
        Assert.Null(copiedPlayer.SoldPrice);
    }

    [Fact]
    public async Task SelectedRosterCopiesIntoNewTournamentUsingDestinationPurse()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var ownerId = Guid.NewGuid();
        db.Users.Add(CreateUser(ownerId, "new-tournament-copy@example.com"));
        await db.SaveChangesAsync();
        var service = new WorkspaceService(db);
        var sourceTournament = await service.CreateTournamentAsync(ownerId, new TournamentRequest
        {
            Name = "Existing Cup",
            Season = "2026",
            Venue = "Pune",
            DefaultTeamPurse = 150000
        }, CancellationToken.None);
        var sourceSeason = Assert.Single((await service.GetWorkspacesAsync(ownerId, CancellationToken.None))
            .Single(workspace => workspace.Tournament.Id == sourceTournament.Id).Seasons);
        var sourceTeam = await service.CreateTeamAsync(ownerId, sourceTournament.Id, new CreateTeamRequest
        {
            Name = "Pune Panthers",
            ShortName = "PUN",
            Captain = "Captain",
            SeasonId = sourceSeason.Id
        }, CancellationToken.None);
        Assert.NotNull(sourceTeam);
        var sourcePlayer = await service.CreatePlayerAsync(ownerId, sourceTournament.Id, new CreatePlayerRequest
        {
            Name = "Returning Player",
            Role = "Bowler",
            BasePrice = 5000,
            SeasonId = sourceSeason.Id
        }, CancellationToken.None);
        Assert.NotNull(sourcePlayer);

        var targetTournament = await service.CreateTournamentAsync(ownerId, new TournamentRequest
        {
            Name = "New Cup",
            Season = "2027",
            Venue = "Mumbai",
            DefaultTeamPurse = 300000
        }, CancellationToken.None);
        var targetSeason = Assert.Single((await service.GetWorkspacesAsync(ownerId, CancellationToken.None))
            .Single(workspace => workspace.Tournament.Id == targetTournament.Id).Seasons);
        var copied = await service.CopyRosterAsync(ownerId, targetTournament.Id, targetSeason.Id, new CopyRosterRequest
        {
            SourceTournamentId = sourceTournament.Id,
            SourceSeasonId = sourceSeason.Id,
            TeamIds = [sourceTeam.Id],
            PlayerIds = [sourcePlayer.Id]
        }, CancellationToken.None);

        Assert.Equal(new CopyRosterResponse(1, 1), copied);
        var targetWorkspace = (await service.GetWorkspacesAsync(ownerId, CancellationToken.None))
            .Single(workspace => workspace.Tournament.Id == targetTournament.Id);
        var copiedTeam = Assert.Single(targetWorkspace.Teams);
        var copiedPlayer = Assert.Single(targetWorkspace.Players);
        Assert.Equal(300000, copiedTeam.Purse);
        Assert.Equal(300000, copiedTeam.InitialPurse);
        Assert.Equal("Available", copiedPlayer.Status);
        Assert.Null(copiedPlayer.TeamId);
        Assert.Null(copiedPlayer.SoldPrice);
    }

    [Fact]
    public async Task LegacyTournamentWideTeamCodeIndexMigratesWithoutLosingPlayerAssignments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var ownerId = Guid.NewGuid();
        db.Users.Add(CreateUser(ownerId, "team-code-migration@example.com"));
        await db.SaveChangesAsync();
        var service = new WorkspaceService(db);
        var tournament = await service.CreateTournamentAsync(ownerId, new TournamentRequest
        {
            Name = "Team Code Migration Cup",
            Season = "2026",
            Venue = "Pune"
        }, CancellationToken.None);
        var sourceSeason = Assert.Single((await service.GetWorkspacesAsync(ownerId, CancellationToken.None))
            .Single().Seasons);
        var sourceTeam = await service.CreateTeamAsync(ownerId, tournament.Id, new CreateTeamRequest
        {
            Name = "Pune Panthers",
            ShortName = "PUN",
            Captain = "Captain",
            SeasonId = sourceSeason.Id
        }, CancellationToken.None);
        Assert.NotNull(sourceTeam);
        var sourcePlayer = await service.CreatePlayerAsync(ownerId, tournament.Id, new CreatePlayerRequest
        {
            Name = "Assigned Player",
            Role = "Batter",
            BasePrice = 10000,
            Status = "Sold",
            TeamId = sourceTeam.Id,
            SoldPrice = 20000,
            SeasonId = sourceSeason.Id
        }, CancellationToken.None);
        Assert.NotNull(sourcePlayer);
        var targetSeason = await service.CreateSeasonAsync(ownerId, tournament.Id, new SeasonRequest
        {
            Name = "2027"
        }, CancellationToken.None);
        Assert.NotNull(targetSeason);

        await db.Database.ExecuteSqlRawAsync("DROP INDEX IX_ManagedTeams_SeasonId_ShortName");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IX_ManagedTeams_TournamentId_ShortName ON ManagedTeams (TournamentId, ShortName)");
        await BusinessSchemaInitializer.InitializeAsync(db);

        var secondSeasonTeam = await service.CreateTeamAsync(ownerId, tournament.Id, new CreateTeamRequest
        {
            Name = "Pune Panthers 2027",
            ShortName = "PUN",
            Captain = "Captain",
            SeasonId = targetSeason.Id
        }, CancellationToken.None);
        Assert.NotNull(secondSeasonTeam);
        var persistedPlayer = await db.Players.SingleAsync(player => player.Id == sourcePlayer.Id);
        Assert.Equal(sourceTeam.Id, persistedPlayer.TeamId);
    }

    [Fact]
    public async Task LegacyAuctionEventSchemaReceivesAuctionStateColumnsWithoutLosingEvents()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var ownerId = Guid.NewGuid();
        db.Users.Add(CreateUser(ownerId, "auction-schema-migration@example.com"));
        await db.SaveChangesAsync();
        var workspaceService = new WorkspaceService(db);
        var tournament = await workspaceService.CreateTournamentAsync(ownerId, new TournamentRequest
        {
            Name = "Auction Schema Migration Cup",
            Season = "2026",
            Venue = "Pune"
        }, CancellationToken.None);
        var season = Assert.Single((await workspaceService.GetWorkspacesAsync(ownerId, CancellationToken.None))
            .Single().Seasons);
        var auctionEvent = await workspaceService.CreateAuctionEventAsync(ownerId, tournament.Id, new AuctionEventRequest
        {
            SeasonId = season.Id,
            Name = "Persisted Event",
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(1)
        }, CancellationToken.None);
        Assert.NotNull(auctionEvent);

        foreach (var sql in new[]
        {
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN CurrentPlayerId",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN LeadingTeamId",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN CurrentBidAmount",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN BidIncrement",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN BidDurationSeconds",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN BidExtensionSeconds",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN BidDeadline",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN PausedRemainingMilliseconds",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN MaximumSquadSize",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN MinimumSquadSize",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN MinimumSquadReservePrice",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN CurrentRound",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN MaximumRounds",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN BidSequence",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN EventSequence",
            "ALTER TABLE ManagedAuctionEvents DROP COLUMN Revision"
        })
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }

        await BusinessSchemaInitializer.InitializeAsync(db);

        var migratedEvent = await db.Auctions.SingleAsync(item => item.Id == auctionEvent.Id);
        Assert.Equal("Persisted Event", migratedEvent.Name);
        Assert.Equal("Scheduled", migratedEvent.Status);
        Assert.Equal(2000, migratedEvent.BidIncrement);
        Assert.Equal(30, migratedEvent.BidDurationSeconds);
        Assert.Equal(25, migratedEvent.MaximumSquadSize);
        Assert.Equal(0, migratedEvent.Revision);
    }

    [Fact]
    public async Task OrganizerCanCreateMultipleIndependentTournaments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var ownerId = Guid.NewGuid();
        db.Users.Add(CreateUser(ownerId, "multi-league@example.com"));
        await db.SaveChangesAsync();

        var service = new WorkspaceService(db);
        var first = await service.CreateTournamentAsync(ownerId, new TournamentRequest
        {
            Name = "City Cup",
            Season = "2026",
            Venue = "Pune"
        }, CancellationToken.None);
        var second = await service.CreateTournamentAsync(ownerId, new TournamentRequest
        {
            Name = "Coastal League",
            Season = "2027",
            Venue = "Mumbai"
        }, CancellationToken.None);

        var workspaces = await service.GetWorkspacesAsync(ownerId, CancellationToken.None);
        Assert.Equal(2, workspaces.Count);
        Assert.Contains(workspaces, workspace => workspace.Tournament.Id == first.Id);
        Assert.Contains(workspaces, workspace => workspace.Tournament.Id == second.Id);
        Assert.NotEqual(
            Assert.Single(workspaces.Single(workspace => workspace.Tournament.Id == first.Id).Seasons).Id,
            Assert.Single(workspaces.Single(workspace => workspace.Tournament.Id == second.Id).Seasons).Id);
    }

    [Fact]
    public async Task LegacyWorkspaceImportPreservesRelationsAndRejectsInvalidDataAtomically()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        db.Users.AddRange(
            CreateUser(ownerId, "importer@example.com"),
            CreateUser(otherOwnerId, "other@example.com"));
        await db.SaveChangesAsync();

        var service = new WorkspaceService(db);
        var imported = await service.ImportWorkspaceAsync(ownerId, new ImportWorkspaceRequest
        {
            Tournament = new TournamentRequest { Name = "Legacy Cup", Season = "2026", Venue = "Pune" },
            Teams =
            [
                new ImportTeamRequest
                {
                    LegacyId = "old-team-1",
                    Name = "Pune Panthers",
                    ShortName = "PUN",
                    Captain = "Captain",
                    InitialPurse = 100000,
                    Purse = 70000,
                    Color = "#FF6A00"
                }
            ],
            Players =
            [
                new ImportPlayerRequest
                {
                    LegacyId = "old-player-1",
                    Name = "Aarav Sharma",
                    City = "Pune",
                    Role = "Batter",
                    BasePrice = 10000,
                    Status = "Sold",
                    TeamLegacyId = "old-team-1",
                    SoldPrice = 30000
                }
            ]
        }, CancellationToken.None);

        var importedTeam = Assert.Single(imported.Teams);
        var importedPlayer = Assert.Single(imported.Players);
        Assert.Equal(70000, importedTeam.Purse);
        Assert.Equal(100000, importedTeam.InitialPurse);
        Assert.Equal("Sold", importedPlayer.Status);
        Assert.Equal(importedTeam.Id, importedPlayer.TeamId);
        Assert.Equal(30000, importedPlayer.SoldPrice);
        Assert.Empty(await service.GetWorkspacesAsync(otherOwnerId, CancellationToken.None));

        await Assert.ThrowsAsync<InvalidWorkspaceImportException>(() => service.ImportWorkspaceAsync(
            otherOwnerId,
            new ImportWorkspaceRequest
            {
                Tournament = new TournamentRequest { Name = "Invalid Cup", Season = "2026", Venue = "Pune" },
                Players =
                [
                    new ImportPlayerRequest
                    {
                        LegacyId = "orphan-player",
                        Name = "Orphan Player",
                        Role = "Bowler",
                        BasePrice = 1000,
                        Status = "Sold",
                        TeamLegacyId = "missing-team",
                        SoldPrice = 2000
                    }
                ]
            },
            CancellationToken.None));
        Assert.Empty(await service.GetWorkspacesAsync(otherOwnerId, CancellationToken.None));
    }

    [Fact]
    public async Task OrganizerWorkspacePersistsAndIsolatedByOwner()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        db.Users.Add(CreateUser(ownerId, "organizer@example.com"));
        db.Users.Add(CreateUser(memberId, "auctioneer@example.com"));
        await db.SaveChangesAsync();

        var service = new WorkspaceService(db);
        var tournament = await service.CreateTournamentAsync(ownerId, new TournamentRequest
        {
            Name = "City Cup",
            Season = "2026",
            Venue = "Pune",
            DefaultTeamPurse = 200000
        }, CancellationToken.None);
        var team = await service.CreateTeamAsync(ownerId, tournament.Id, new CreateTeamRequest
        {
            Name = "Pune Panthers",
            ShortName = "PUN",
            DisplayName = "Panthers",
            Captain = "Captain"
        }, CancellationToken.None);
        Assert.NotNull(team);
        Assert.Equal("Panthers", team.DisplayName);
        Assert.NotEqual(Guid.Empty, team.SeasonId);
        Assert.Equal(200000, team.InitialPurse);
        Assert.Matches("^#[0-9A-F]{6}$", team.Color);
        var secondTeam = await service.CreateTeamAsync(ownerId, tournament.Id, new CreateTeamRequest
        {
            Name = "Mumbai Strikers",
            ShortName = "MUM",
            Captain = "Another Captain"
        }, CancellationToken.None);
        Assert.NotNull(secondTeam);
        Assert.NotEqual(team.Color, secondTeam.Color);
        Assert.NotEqual(team.ShortName, secondTeam.ShortName);
        var player = await service.CreatePlayerAsync(ownerId, tournament.Id, new CreatePlayerRequest
        {
            Name = "Aarav Sharma",
            City = "Pune",
            Email = "aarav@example.com",
            Nationality = "India",
            DateOfBirth = "2000-01-02",
            Role = "Batter",
            BasePrice = 10000
        }, CancellationToken.None);
        Assert.NotNull(player);
        var purchasedPlayer = await service.CreatePlayerAsync(ownerId, tournament.Id, new CreatePlayerRequest
        {
            Name = "Bought Player",
            City = "Pune",
            Role = "Bowler",
            BasePrice = 5000,
            Status = "Sold",
            TeamId = team.Id,
            SoldPrice = 30000
        }, CancellationToken.None);
        Assert.NotNull(purchasedPlayer);
        await Assert.ThrowsAsync<InsufficientTeamPurseException>(() => service.CreatePlayerAsync(
            ownerId,
            tournament.Id,
            new CreatePlayerRequest
            {
                Name = "Over-budget Player",
                Role = "Batter",
                BasePrice = 1000,
                Status = "Sold",
                TeamId = team.Id,
                SoldPrice = 200000
            },
            CancellationToken.None));

        var workspaces = await service.GetWorkspacesAsync(ownerId, CancellationToken.None);
        Assert.Single(workspaces);
        Assert.Equal("City Cup", workspaces[0].Tournament.Name);
        Assert.Equal(2, workspaces[0].Teams.Count);
        Assert.Equal(2, workspaces[0].Players.Count);
        Assert.Equal(player.Id, workspaces[0].Players.Single(item => item.Status == "Available").Id);
        Assert.Equal(170000, workspaces[0].Teams.Single(item => item.Id == team.Id).Purse);
        Assert.Equal(purchasedPlayer.Id, workspaces[0].Players.Single(item => item.Status == "Sold").Id);
        var initialSeason = Assert.Single(workspaces[0].Seasons);
        Assert.Equal("2026", initialSeason.Name);
        Assert.Equal(2, initialSeason.TeamCount);
        Assert.Equal(2, initialSeason.PlayerCount);
        Assert.Equal(team.SeasonId, initialSeason.Id);
        var addedMember = await service.AddLeagueMemberAsync(ownerId, tournament.Id, new AddLeagueMemberRequest
        {
            Email = "auctioneer@example.com",
            Role = "Auctioneer"
        }, CancellationToken.None);
        Assert.NotNull(addedMember);
        Assert.Equal(memberId, addedMember.UserId);
        Assert.Equal("Auctioneer", addedMember.Role);
        Assert.Single(await service.GetWorkspacesAsync(memberId, CancellationToken.None));
        var memberTeam = await service.CreateTeamAsync(memberId, tournament.Id, new CreateTeamRequest
        {
            Name = "League Auction Team",
            ShortName = "LAT",
            Captain = "Member Captain",
            SeasonId = initialSeason.Id
        }, CancellationToken.None);
        Assert.NotNull(memberTeam);
        var eventResponse = await service.CreateAuctionEventAsync(ownerId, tournament.Id, new AuctionEventRequest
        {
            SeasonId = initialSeason.Id,
            Name = "Season Auction",
            ScheduledAt = new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero),
            Venue = "Pune",
            Description = "Opening auction"
        }, CancellationToken.None);
        Assert.NotNull(eventResponse);
        Assert.Equal("Scheduled", eventResponse.Status);
        var updatedEvent = await service.UpdateAuctionEventAsync(ownerId, tournament.Id, eventResponse.Id, new AuctionEventRequest
        {
            SeasonId = initialSeason.Id,
            Name = "Season Auction Updated",
            ScheduledAt = eventResponse.ScheduledAt.AddDays(1),
            Venue = "Mumbai",
            Status = "Scheduled"
        }, CancellationToken.None);
        Assert.Equal("Scheduled", updatedEvent?.Status);
        Assert.True(await service.DeleteAuctionEventAsync(ownerId, tournament.Id, eventResponse.Id, CancellationToken.None));
        Assert.True(await service.RemoveLeagueMemberAsync(ownerId, tournament.Id, memberId, CancellationToken.None));
        var nextSeason = await service.CreateSeasonAsync(ownerId, tournament.Id, new SeasonRequest
        {
            Name = "2027",
            StartDate = new DateOnly(2027, 1, 1),
            EndDate = new DateOnly(2027, 12, 31),
            Venue = "Mumbai"
        }, CancellationToken.None);
        Assert.NotNull(nextSeason);
        var nextTeam = await service.CreateTeamAsync(ownerId, tournament.Id, new CreateTeamRequest
        {
            Name = "Mumbai United",
            ShortName = "MUN",
            Captain = "New Captain",
            SeasonId = nextSeason.Id
        }, CancellationToken.None);
        Assert.NotNull(nextTeam);
        Assert.Equal(nextSeason.Id, nextTeam.SeasonId);
        Assert.False(await service.DeleteSeasonAsync(ownerId, tournament.Id, nextSeason.Id, CancellationToken.None));
        var updatedWorkspaces = await service.GetWorkspacesAsync(ownerId, CancellationToken.None);
        Assert.Equal(2, updatedWorkspaces[0].Seasons.Count);
        Assert.Equal(1, updatedWorkspaces[0].Seasons.Single(item => item.Id == nextSeason.Id).TeamCount);
        Assert.Equal("aarav@example.com", updatedWorkspaces[0].Players.Single(item => item.Id == player.Id).Email);
        Assert.Empty(await service.GetWorkspacesAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.True(await service.DeleteTournamentAsync(ownerId, tournament.Id, CancellationToken.None));
        Assert.Empty(await service.GetWorkspacesAsync(ownerId, CancellationToken.None));
    }

    private static ApplicationUser CreateUser(Guid id, string email) => new()
    {
        Id = id,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        DisplayName = "Organizer"
    };
}
