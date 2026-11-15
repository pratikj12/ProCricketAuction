using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProCricketAuction.Api.DTOs;
using ProCricketAuction.Api.Services;
using ProCricketAuction.Domain.Entities;
using ProCricketAuction.Infrastructure.Data;
using Xunit;

namespace ProCricketAuction.IntegrationTests;

public sealed class AuctionPersistenceTests
{
    [Fact]
    public async Task PublicAuctionStateIsReadOnlyAndExposesOnlyDisplayFields()
    {
        await using var fixture = await AuctionFixture.CreateAsync(playerCount: 2);

        Assert.Null(await fixture.AuctionService.GetPublicStateAsync(
            fixture.AuctionId, CancellationToken.None));

        await fixture.AuctionService.StartAsync(
            fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        await fixture.AuctionService.PresentPlayerAsync(
            fixture.OwnerId,
            fixture.AuctionId,
            new PresentAuctionPlayerRequest { PlayerId = fixture.PlayerIds[0] },
            CancellationToken.None);

        var publicState = await fixture.AuctionService.GetPublicStateAsync(
            fixture.AuctionId, CancellationToken.None);

        Assert.NotNull(publicState);
        Assert.Null(publicState.AssignedTeamId);
        Assert.Empty(publicState.AssignedTeamIds);
        Assert.Single(publicState.Players);
        Assert.Equal(fixture.PlayerIds[0], publicState.Players[0].Id);
        Assert.Empty(publicState.RecentAuditEvents);
        Assert.All(publicState.Teams, team =>
        {
            Assert.False(team.CanBid);
            Assert.Equal(0, team.MaximumAffordableBid);
        });
    }

    [Fact]
    public async Task AcceptedBidIsIdempotentAndSaleDeductsTheTeamPurseOnce()
    {
        await using var fixture = await AuctionFixture.CreateAsync(playerCount: 2);
        var started = await fixture.AuctionService.StartAsync(fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        Assert.NotNull(started);
        var presented = await fixture.AuctionService.PresentPlayerAsync(
            fixture.OwnerId,
            fixture.AuctionId,
            new PresentAuctionPlayerRequest { PlayerId = fixture.PlayerIds[0] },
            CancellationToken.None);
        Assert.NotNull(presented);

        var request = new PlaceAuctionBidRequest
        {
            TeamId = fixture.TeamIds[0],
            Amount = 12000,
            ClientRequestId = Guid.NewGuid(),
            ExpectedRevision = presented.Revision
        };
        var accepted = await fixture.AuctionService.PlaceBidAsync(
            fixture.CaptainIds[0], fixture.AuctionId, request, CancellationToken.None);
        var retried = await fixture.AuctionService.PlaceBidAsync(
            fixture.CaptainIds[0], fixture.AuctionId, request, CancellationToken.None);

        Assert.NotNull(accepted);
        Assert.NotNull(retried);
        Assert.Equal(12000, accepted.CurrentBidAmount);
        Assert.Equal(12000, retried.CurrentBidAmount);
        Assert.Equal(1, await fixture.Db.Bids.CountAsync(bid => bid.Accepted));
        Assert.Equal("BidAccepted", Assert.Single(await fixture.Db.AuditLogs
            .Where(audit => audit.EventType == "BidAccepted").ToListAsync()).EventType);

        var sold = await fixture.AuctionService.MarkSoldAsync(
            fixture.OwnerId, fixture.AuctionId,
            new ConfirmAuctionSaleRequest { ExpectedRevision = accepted.Revision },
            CancellationToken.None);

        Assert.NotNull(sold);
        Assert.Equal(88000, (await fixture.Db.Teams.SingleAsync(team => team.Id == fixture.TeamIds[0])).Purse);
        var purchasedPlayer = await fixture.Db.Players.SingleAsync(player => player.Id == fixture.PlayerIds[0]);
        Assert.Equal("Sold", purchasedPlayer.Status);
        Assert.Equal(fixture.TeamIds[0], purchasedPlayer.TeamId);
        Assert.Equal(12000, purchasedPlayer.SoldPrice);
        Assert.Single(await fixture.Db.Purchases.Where(purchase => !purchase.IsReversed).ToListAsync());
        var workspace = Assert.Single(await new WorkspaceService(fixture.Db)
            .GetWorkspacesAsync(fixture.OwnerId, CancellationToken.None));
        var season = Assert.Single(workspace.Seasons);
        Assert.Equal(1, season.SoldPlayerCount);
        Assert.Equal(12000, season.TotalSpend);
        Assert.Equal(88000, Assert.Single(workspace.Teams, item => item.Id == fixture.TeamIds[0]).Purse);
        Assert.Equal("Sold", Assert.Single(workspace.Players, item => item.Id == fixture.PlayerIds[0]).Status);
        var nextPlayer = await fixture.AuctionService.NextPlayerAsync(
            fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        Assert.NotNull(nextPlayer);
        Assert.Empty(nextPlayer.RecentBids);
        Assert.Equal(12000, Assert.Single(nextPlayer.RecentAuctionBids).Amount);
        await Assert.ThrowsAsync<AuctionCommandRejectedException>(() =>
            fixture.AuctionService.MarkSoldAsync(
                fixture.OwnerId,
                fixture.AuctionId,
                new ConfirmAuctionSaleRequest { ExpectedRevision = sold.Revision },
                CancellationToken.None));
        Assert.Equal(88000, (await fixture.Db.Teams.SingleAsync(team => team.Id == fixture.TeamIds[0])).Purse);
    }

    [Fact]
    public async Task InvalidAndUnassignedCaptainBidsAreRejectedAndAudited()
    {
        await using var fixture = await AuctionFixture.CreateAsync(teamCount: 2);
        await fixture.AuctionService.StartAsync(fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        var state = await fixture.AuctionService.PresentPlayerAsync(
            fixture.OwnerId,
            fixture.AuctionId,
            new PresentAuctionPlayerRequest { PlayerId = fixture.PlayerIds[0] },
            CancellationToken.None);
        Assert.NotNull(state);

        await Assert.ThrowsAsync<AuctionPermissionException>(() => fixture.AuctionService.PlaceBidAsync(
            fixture.CaptainIds[0],
            fixture.AuctionId,
            new PlaceAuctionBidRequest
            {
                TeamId = fixture.TeamIds[1],
                Amount = 10000,
                ClientRequestId = Guid.NewGuid(),
                ExpectedRevision = state.Revision
            },
            CancellationToken.None));

        var rejection = await Assert.ThrowsAsync<AuctionBidRejectedException>(() =>
            fixture.AuctionService.PlaceBidAsync(
                fixture.CaptainIds[0],
                fixture.AuctionId,
                new PlaceAuctionBidRequest
                {
                    TeamId = fixture.TeamIds[0],
                    Amount = 9999,
                    ClientRequestId = Guid.NewGuid(),
                    ExpectedRevision = state.Revision
                },
                CancellationToken.None));

        Assert.Contains("next valid bid", rejection.Message, StringComparison.OrdinalIgnoreCase);
        var rejectedBid = Assert.Single(await fixture.Db.Bids.Where(bid => !bid.Accepted).ToListAsync());
        Assert.Equal("BidRejected", Assert.Single(await fixture.Db.AuditLogs
            .Where(audit => audit.EventType == "BidRejected").ToListAsync()).EventType);
        Assert.Equal(100000, (await fixture.Db.Teams.SingleAsync(team => team.Id == fixture.TeamIds[0])).Purse);
        Assert.Equal(0, rejectedBid.SequenceNumber);
    }

    [Fact]
    public async Task ServerRejectsBidsAbovePurseAndAtSquadLimit()
    {
        await using (var purseFixture = await AuctionFixture.CreateAsync())
        {
            var team = await purseFixture.Db.Teams.SingleAsync(item => item.Id == purseFixture.TeamIds[0]);
            team.Purse = 11000;
            await purseFixture.Db.SaveChangesAsync();
            await purseFixture.AuctionService.StartAsync(purseFixture.OwnerId, purseFixture.AuctionId, CancellationToken.None);
            var state = await purseFixture.AuctionService.PresentPlayerAsync(
                purseFixture.OwnerId,
                purseFixture.AuctionId,
                new PresentAuctionPlayerRequest { PlayerId = purseFixture.PlayerIds[0] },
                CancellationToken.None);
            Assert.NotNull(state);

            await Assert.ThrowsAsync<AuctionBidRejectedException>(() =>
                purseFixture.AuctionService.PlaceBidAsync(
                    purseFixture.CaptainIds[0],
                    purseFixture.AuctionId,
                    new PlaceAuctionBidRequest
                    {
                        TeamId = purseFixture.TeamIds[0],
                        Amount = 12000,
                        ClientRequestId = Guid.NewGuid(),
                        ExpectedRevision = state.Revision
                    },
                    CancellationToken.None));
            Assert.Empty(await purseFixture.Db.Bids.Where(bid => bid.Accepted).ToListAsync());
        }

        await using var squadFixture = await AuctionFixture.CreateAsync(playerCount: 2);
        var purchasedPlayer = await squadFixture.Db.Players.SingleAsync(item => item.Id == squadFixture.PlayerIds[0]);
        purchasedPlayer.Status = "Sold";
        purchasedPlayer.TeamId = squadFixture.TeamIds[0];
        purchasedPlayer.SoldPrice = 10000;
        var auction = await squadFixture.Db.Auctions.SingleAsync(item => item.Id == squadFixture.AuctionId);
        auction.MaximumSquadSize = 1;
        await squadFixture.Db.SaveChangesAsync();
        await squadFixture.AuctionService.StartAsync(squadFixture.OwnerId, squadFixture.AuctionId, CancellationToken.None);
        var presented = await squadFixture.AuctionService.PresentPlayerAsync(
            squadFixture.OwnerId,
            squadFixture.AuctionId,
            new PresentAuctionPlayerRequest { PlayerId = squadFixture.PlayerIds[1] },
            CancellationToken.None);
        Assert.NotNull(presented);

        await Assert.ThrowsAsync<AuctionBidRejectedException>(() =>
            squadFixture.AuctionService.PlaceBidAsync(
                squadFixture.CaptainIds[0],
                squadFixture.AuctionId,
                new PlaceAuctionBidRequest
                {
                    TeamId = squadFixture.TeamIds[0],
                    Amount = 10000,
                    ClientRequestId = Guid.NewGuid(),
                    ExpectedRevision = presented.Revision
                },
                CancellationToken.None));
        Assert.Empty(await squadFixture.Db.Bids.Where(bid => bid.Accepted).ToListAsync());
    }

    [Fact]
    public async Task AuthorizedSaleReversalRestoresPurseAndAuditsTheCorrection()
    {
        await using var fixture = await AuctionFixture.CreateAsync();
        await fixture.AuctionService.StartAsync(fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        var presented = await fixture.AuctionService.PresentPlayerAsync(
            fixture.OwnerId,
            fixture.AuctionId,
            new PresentAuctionPlayerRequest { PlayerId = fixture.PlayerIds[0] },
            CancellationToken.None);
        Assert.NotNull(presented);
        var bid = await fixture.AuctionService.PlaceBidAsync(
            fixture.CaptainIds[0],
            fixture.AuctionId,
            new PlaceAuctionBidRequest
            {
                TeamId = fixture.TeamIds[0],
                Amount = 12000,
                ClientRequestId = Guid.NewGuid(),
                ExpectedRevision = presented.Revision
            },
            CancellationToken.None);
        Assert.NotNull(bid);
        var sale = await fixture.AuctionService.MarkSoldAsync(
            fixture.OwnerId,
            fixture.AuctionId,
            new ConfirmAuctionSaleRequest { ExpectedRevision = bid.Revision },
            CancellationToken.None);
        Assert.NotNull(sale);
        var purchase = await fixture.Db.Purchases.SingleAsync();

        var corrected = await fixture.AuctionService.ReverseSaleAsync(
            fixture.OwnerId,
            fixture.AuctionId,
            new ReverseAuctionSaleRequest
            {
                PurchaseId = purchase.Id,
                Reason = "Corrected an operator sale error.",
                ExpectedRevision = sale.Revision
            },
            CancellationToken.None);

        Assert.NotNull(corrected);
        Assert.Equal("Paused", corrected.Status);
        Assert.Equal(100000, (await fixture.Db.Teams.SingleAsync(team => team.Id == fixture.TeamIds[0])).Purse);
        Assert.Equal("Available", (await fixture.Db.Players.SingleAsync(player => player.Id == fixture.PlayerIds[0])).Status);
        Assert.True((await fixture.Db.Purchases.SingleAsync()).IsReversed);
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), audit => audit.EventType == "SaleReversed");
    }

    [Fact]
    public async Task UnsoldPlayersDoNotReducePurseAndCanReturnInAConfiguredLaterRound()
    {
        await using var fixture = await AuctionFixture.CreateAsync(playerCount: 2);
        await fixture.AuctionService.StartAsync(fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        var first = await fixture.AuctionService.PresentPlayerAsync(
            fixture.OwnerId,
            fixture.AuctionId,
            new PresentAuctionPlayerRequest { PlayerId = fixture.PlayerIds[0] },
            CancellationToken.None);
        Assert.NotNull(first);
        await fixture.AuctionService.MarkUnsoldAsync(fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        Assert.Equal(100000, (await fixture.Db.Teams.SingleAsync(team => team.Id == fixture.TeamIds[0])).Purse);

        await fixture.AuctionService.NextPlayerAsync(fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        await fixture.AuctionService.MarkUnsoldAsync(fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        await fixture.AuctionService.NextPlayerAsync(fixture.OwnerId, fixture.AuctionId, CancellationToken.None);

        var nextRound = await fixture.AuctionService.StartNextRoundAsync(
            fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        Assert.NotNull(nextRound);
        Assert.Equal(2, nextRound.CurrentRound);
        Assert.Equal("Available", (await fixture.Db.Players.SingleAsync(
            player => player.Id == fixture.PlayerIds[0])).Status);
        Assert.Equal(fixture.PlayerIds[0], nextRound.CurrentPlayer?.Id);
    }

    [Fact]
    public async Task ConcurrentCaptainsCannotBothSubmitAgainstTheSameAuctionRevision()
    {
        await using var fixture = await AuctionFixture.CreateAsync(teamCount: 2);
        await fixture.AuctionService.StartAsync(fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        var presented = await fixture.AuctionService.PresentPlayerAsync(
            fixture.OwnerId,
            fixture.AuctionId,
            new PresentAuctionPlayerRequest { PlayerId = fixture.PlayerIds[0] },
            CancellationToken.None);
        Assert.NotNull(presented);

        var secondConnection = new SqliteConnection(fixture.ConnectionString);
        await secondConnection.OpenAsync();
        var secondOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(secondConnection)
            .Options;
        await using var secondDb = new ApplicationDbContext(secondOptions);
        var secondService = new AuctionService(secondDb, new NoopAuctionNotifier(),
            NullLogger<AuctionService>.Instance);
        var firstTask = fixture.AuctionService.PlaceBidAsync(
            fixture.CaptainIds[0],
            fixture.AuctionId,
            new PlaceAuctionBidRequest
            {
                TeamId = fixture.TeamIds[0],
                Amount = 12000,
                ClientRequestId = Guid.NewGuid(),
                ExpectedRevision = presented.Revision
            },
            CancellationToken.None);
        var secondTask = secondService.PlaceBidAsync(
            fixture.CaptainIds[1],
            fixture.AuctionId,
            new PlaceAuctionBidRequest
            {
                TeamId = fixture.TeamIds[1],
                Amount = 14000,
                ClientRequestId = Guid.NewGuid(),
                ExpectedRevision = presented.Revision
            },
            CancellationToken.None);

        try
        {
            await Task.WhenAll(firstTask, secondTask);
        }
        catch (AuctionCommandRejectedException)
        {
        }

        Assert.Equal(1, await fixture.Db.Bids.CountAsync(bid => bid.Accepted));
        Assert.Equal(1, await fixture.Db.Bids.CountAsync(bid => !bid.Accepted));
        var latest = await fixture.AuctionService.GetStateAsync(
            fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        Assert.NotNull(latest);
        Assert.Equal(1, latest.AcceptedBidCount);
    }

    [Fact]
    public async Task DeadlineExpiryRejectsLateBidsAndSaleCanAdvanceAfterAuctioneerConfirmation()
    {
        await using var fixture = await AuctionFixture.CreateAsync(teamCount: 2, playerCount: 2);
        await fixture.AuctionService.StartAsync(fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        var presented = await fixture.AuctionService.PresentPlayerAsync(
            fixture.OwnerId,
            fixture.AuctionId,
            new PresentAuctionPlayerRequest { PlayerId = fixture.PlayerIds[0] },
            CancellationToken.None);
        Assert.NotNull(presented);
        var accepted = await fixture.AuctionService.PlaceBidAsync(
            fixture.CaptainIds[0],
            fixture.AuctionId,
            new PlaceAuctionBidRequest
            {
                TeamId = fixture.TeamIds[0],
                Amount = 10000,
                ClientRequestId = Guid.NewGuid(),
                ExpectedRevision = presented.Revision
            },
            CancellationToken.None);
        Assert.NotNull(accepted);

        var auction = await fixture.Db.Auctions.SingleAsync(item => item.Id == fixture.AuctionId);
        auction.BidDeadline = DateTimeOffset.UtcNow.AddSeconds(-1);
        await fixture.Db.SaveChangesAsync();
        await fixture.AuctionService.ExpireDueAuctionsAsync(CancellationToken.None);
        var expired = await fixture.AuctionService.GetStateAsync(
            fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        Assert.NotNull(expired);
        Assert.Equal("AwaitingDecision", expired.Status);

        await Assert.ThrowsAsync<AuctionBidRejectedException>(() =>
            fixture.AuctionService.PlaceBidAsync(
                fixture.CaptainIds[1],
                fixture.AuctionId,
                new PlaceAuctionBidRequest
                {
                    TeamId = fixture.TeamIds[1],
                    Amount = 12000,
                    ClientRequestId = Guid.NewGuid(),
                    ExpectedRevision = expired.Revision
                },
                CancellationToken.None));

        var latest = await fixture.AuctionService.GetStateAsync(
            fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        Assert.NotNull(latest);
        var sold = await fixture.AuctionService.MarkSoldAsync(
            fixture.OwnerId,
            fixture.AuctionId,
            new ConfirmAuctionSaleRequest { ExpectedRevision = latest.Revision },
            CancellationToken.None);
        Assert.NotNull(sold);
        Assert.Equal("Live", sold.Status);

        var next = await fixture.AuctionService.NextPlayerAsync(
            fixture.OwnerId, fixture.AuctionId, CancellationToken.None);
        Assert.NotNull(next);
        Assert.Equal(fixture.PlayerIds[1], next.CurrentPlayer?.Id);
        Assert.Equal(90000, (await fixture.Db.Teams.SingleAsync(team => team.Id == fixture.TeamIds[0])).Purse);
    }

    private sealed class AuctionFixture : IAsyncDisposable
    {
        private AuctionFixture(
            SqliteConnection connection,
            string connectionString,
            ApplicationDbContext db,
            Guid ownerId,
            Guid[] captainIds,
            Guid[] teamIds,
            Guid[] playerIds,
            Guid auctionId,
            AuctionService auctionService)
        {
            Connection = connection;
            ConnectionString = connectionString;
            Db = db;
            OwnerId = ownerId;
            CaptainIds = captainIds;
            TeamIds = teamIds;
            PlayerIds = playerIds;
            AuctionId = auctionId;
            AuctionService = auctionService;
        }

        private SqliteConnection Connection { get; }
        public string ConnectionString { get; }
        public ApplicationDbContext Db { get; }
        public Guid OwnerId { get; }
        public Guid[] CaptainIds { get; }
        public Guid[] TeamIds { get; }
        public Guid[] PlayerIds { get; }
        public Guid AuctionId { get; }
        public AuctionService AuctionService { get; }

        public static async Task<AuctionFixture> CreateAsync(int teamCount = 1, int playerCount = 1)
        {
            var connectionString = $"Data Source=AuctionTest-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var ownerId = Guid.NewGuid();
            var captainIds = Enumerable.Range(0, teamCount).Select(_ => Guid.NewGuid()).ToArray();
            db.Users.Add(CreateUser(ownerId, "auction-owner@example.com", "Owner"));
            db.Users.AddRange(captainIds.Select((id, index) =>
                CreateUser(id, $"captain-{index}@example.com", $"Captain {index}")));
            await db.SaveChangesAsync();

            var workspaceService = new WorkspaceService(db);
            var tournament = await workspaceService.CreateTournamentAsync(ownerId, new TournamentRequest
            {
                Name = "Authoritative Auction Cup",
                Season = "2026",
                Venue = "Pune",
                DefaultTeamPurse = 100000
            }, CancellationToken.None);
            var workspace = Assert.Single(await workspaceService.GetWorkspacesAsync(ownerId, CancellationToken.None));
            var season = Assert.Single(workspace.Seasons);
            var teamIds = new Guid[teamCount];
            for (var index = 0; index < teamCount; index++)
            {
                var team = await workspaceService.CreateTeamAsync(ownerId, tournament.Id, new CreateTeamRequest
                {
                    Name = $"Team {index}",
                    ShortName = $"T{index}",
                    Captain = $"Captain {index}",
                    SeasonId = season.Id
                }, CancellationToken.None);
                Assert.NotNull(team);
                teamIds[index] = team.Id;
                var member = await workspaceService.AddLeagueMemberAsync(ownerId, tournament.Id, new AddLeagueMemberRequest
                {
                    Email = $"captain-{index}@example.com",
                    Role = "Captain",
                    TeamIds = [team.Id]
                }, CancellationToken.None);
                Assert.NotNull(member);
            }

            var playerIds = new Guid[playerCount];
            for (var index = 0; index < playerCount; index++)
            {
                var player = await workspaceService.CreatePlayerAsync(ownerId, tournament.Id, new CreatePlayerRequest
                {
                    Name = $"Player {index}",
                    Role = "Batter",
                    BasePrice = 10000,
                    SeasonId = season.Id
                }, CancellationToken.None);
                Assert.NotNull(player);
                playerIds[index] = player.Id;
            }

            var auctionEvent = await workspaceService.CreateAuctionEventAsync(ownerId, tournament.Id, new AuctionEventRequest
            {
                SeasonId = season.Id,
                Name = "Opening Auction",
                ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(5)
            }, CancellationToken.None);
            Assert.NotNull(auctionEvent);
            var auctionService = new AuctionService(db, new NoopAuctionNotifier(),
                NullLogger<AuctionService>.Instance);
            return new AuctionFixture(connection, connectionString, db, ownerId, captainIds, teamIds, playerIds,
                auctionEvent.Id, auctionService);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private static ApplicationUser CreateUser(Guid id, string email, string displayName) => new()
    {
        Id = id,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        EmailConfirmed = true,
        DisplayName = displayName
    };

    private sealed class NoopAuctionNotifier : IAuctionNotifier
    {
        public Task PublishAsync(Guid auctionId, AuctionStateResponse state, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
