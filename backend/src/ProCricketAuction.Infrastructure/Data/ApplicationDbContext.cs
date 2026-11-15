using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProCricketAuction.Domain.Entities;

namespace ProCricketAuction.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<LeagueMember> LeagueMembers => Set<LeagueMember>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<TournamentSeason> TournamentSeasons => Set<TournamentSeason>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Auction> Auctions => Set<Auction>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tournament>(entity =>
        {
            entity.ToTable("ManagedTournaments");
            entity.HasKey(tournament => tournament.Id);
            entity.Property(tournament => tournament.Name).HasMaxLength(160).IsRequired();
            entity.Property(tournament => tournament.Season).HasMaxLength(40);
            entity.Property(tournament => tournament.Venue).HasMaxLength(160);
            entity.Property(tournament => tournament.DefaultTeamPurse).HasDefaultValue(100000L);
            entity.Property(tournament => tournament.CurrencyCode).HasMaxLength(3).HasDefaultValue("INR").IsRequired();
            entity.Property(tournament => tournament.Status).HasMaxLength(24).IsRequired();
            entity.HasIndex(tournament => new { tournament.OwnerId, tournament.CreatedAt });
            entity.HasOne(tournament => tournament.Owner)
                .WithMany()
                .HasForeignKey(tournament => tournament.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Team>(entity =>
        {
            entity.ToTable("ManagedTeams");
            entity.HasKey(team => team.Id);
            entity.Property(team => team.Name).HasMaxLength(100).IsRequired();
            entity.Property(team => team.ShortName).HasMaxLength(8).IsRequired();
            entity.Property(team => team.DisplayName).HasMaxLength(100);
            entity.Property(team => team.Captain).HasMaxLength(100).IsRequired();
            entity.Property(team => team.City).HasMaxLength(120);
            entity.Property(team => team.LogoDataUrl).HasMaxLength(350000);
            entity.Property(team => team.Color).HasMaxLength(16).IsRequired();
            entity.HasIndex(team => new { team.SeasonId, team.ShortName }).IsUnique();
            entity.HasOne(team => team.Tournament)
                .WithMany(tournament => tournament.Teams)
                .HasForeignKey(team => team.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(team => team.Season)
                .WithMany(season => season.Teams)
                .HasForeignKey(team => team.SeasonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Player>(entity =>
        {
            entity.ToTable("ManagedPlayers");
            entity.HasKey(player => player.Id);
            entity.Property(player => player.FullName).HasMaxLength(120).IsRequired();
            entity.Property(player => player.City).HasMaxLength(120);
            entity.Property(player => player.Role).HasMaxLength(32).IsRequired();
            entity.Property(player => player.Email).HasMaxLength(254);
            entity.Property(player => player.Nationality).HasMaxLength(80);
            entity.Property(player => player.DateOfBirth).HasMaxLength(10);
            entity.Property(player => player.PhotoDataUrl).HasMaxLength(700000);
            entity.Property(player => player.Status).HasMaxLength(24).IsRequired();
            entity.HasIndex(player => new { player.TournamentId, player.FullName });
            entity.HasOne(player => player.Tournament)
                .WithMany(tournament => tournament.Players)
                .HasForeignKey(player => player.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(player => player.Team)
                .WithMany()
                .HasForeignKey(player => player.TeamId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(player => player.Season)
                .WithMany(season => season.Players)
                .HasForeignKey(player => player.SeasonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TournamentSeason>(entity =>
        {
            entity.ToTable("ManagedSeasons");
            entity.HasKey(season => season.Id);
            entity.Property(season => season.Name).HasMaxLength(80).IsRequired();
            entity.Property(season => season.Venue).HasMaxLength(160);
            entity.Property(season => season.Status).HasMaxLength(24).IsRequired();
            entity.HasIndex(season => new { season.TournamentId, season.Name }).IsUnique();
            entity.HasOne(season => season.Tournament)
                .WithMany(tournament => tournament.Seasons)
                .HasForeignKey(season => season.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LeagueMember>(entity =>
        {
            entity.ToTable("ManagedLeagueMembers");
            entity.HasKey(member => new { member.TournamentId, member.UserId });
            entity.Property(member => member.Role).HasMaxLength(24).IsRequired();
            entity.HasOne(member => member.Tournament)
                .WithMany(tournament => tournament.Members)
                .HasForeignKey(member => member.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(member => member.User)
                .WithMany()
                .HasForeignKey(member => member.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TeamMember>(entity =>
        {
            entity.ToTable("ManagedTeamMembers");
            entity.HasKey(member => new { member.TeamId, member.UserId });
            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(member => member.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(member => member.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Auction>(entity =>
        {
            entity.ToTable("ManagedAuctionEvents");
            entity.HasKey(auction => auction.Id);
            entity.Property(auction => auction.Name).HasMaxLength(120).IsRequired();
            entity.Property(auction => auction.Venue).HasMaxLength(160);
            entity.Property(auction => auction.Description).HasMaxLength(1000);
            entity.Property(auction => auction.Status).HasMaxLength(24).IsRequired();
            entity.Property(auction => auction.Revision).IsConcurrencyToken();
            entity.HasIndex(auction => new { auction.SeasonId, auction.ScheduledAt });
            entity.HasOne(auction => auction.Tournament)
                .WithMany(tournament => tournament.AuctionEvents)
                .HasForeignKey(auction => auction.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(auction => auction.Season)
                .WithMany(season => season.AuctionEvents)
                .HasForeignKey(auction => auction.SeasonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Bid>(entity =>
        {
            entity.ToTable("AuctionBids");
            entity.HasKey(bid => bid.Id);
            entity.Property(bid => bid.RejectionReason).HasMaxLength(240);
            entity.HasIndex(bid => new { bid.AuctionId, bid.ClientRequestId }).IsUnique();
            entity.HasIndex(bid => new { bid.AuctionId, bid.PlayerId, bid.SequenceNumber });
            entity.HasOne(bid => bid.Auction).WithMany(auction => auction.Bids)
                .HasForeignKey(bid => bid.AuctionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(bid => bid.Player).WithMany()
                .HasForeignKey(bid => bid.PlayerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(bid => bid.Team).WithMany()
                .HasForeignKey(bid => bid.TeamId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(bid => bid.User).WithMany()
                .HasForeignKey(bid => bid.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Purchase>(entity =>
        {
            entity.ToTable("AuctionPurchases");
            entity.HasKey(purchase => purchase.Id);
            entity.Property(purchase => purchase.CorrectionReason).HasMaxLength(500);
            entity.HasIndex(purchase => purchase.PlayerId)
                .IsUnique()
                .HasFilter("IsReversed = 0");
            entity.HasOne(purchase => purchase.Auction).WithMany(auction => auction.Purchases)
                .HasForeignKey(purchase => purchase.AuctionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(purchase => purchase.Player).WithMany()
                .HasForeignKey(purchase => purchase.PlayerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(purchase => purchase.Team).WithMany()
                .HasForeignKey(purchase => purchase.TeamId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuctionAuditEvents");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.EventType).HasMaxLength(48).IsRequired();
            entity.Property(audit => audit.Details).HasMaxLength(2000);
            entity.HasIndex(audit => new { audit.AuctionId, audit.EventSequence }).IsUnique();
            entity.HasOne(audit => audit.Auction).WithMany(auction => auction.AuditEvents)
                .HasForeignKey(audit => audit.AuctionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(audit => audit.Actor).WithMany()
                .HasForeignKey(audit => audit.ActorUserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
