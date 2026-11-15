using Microsoft.EntityFrameworkCore;

namespace ProCricketAuction.Infrastructure.Data;

public static class BusinessSchemaInitializer
{
    public static async Task InitializeAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS ManagedTournaments (
                    Id TEXT NOT NULL PRIMARY KEY,
                    OwnerId TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Season TEXT NOT NULL,
                    Venue TEXT NOT NULL,
                    DefaultTeamPurse INTEGER NOT NULL DEFAULT 100000,
                    CurrencyCode TEXT NOT NULL DEFAULT 'INR',
                    MaximumTeams INTEGER NULL,
                    MaximumPlayers INTEGER NULL,
                    Status TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (OwnerId) REFERENCES AspNetUsers (Id) ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS IX_ManagedTournaments_OwnerId_CreatedAt
                    ON ManagedTournaments (OwnerId, CreatedAt);
                CREATE TABLE IF NOT EXISTS ManagedSeasons (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TournamentId TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    StartDate TEXT NULL,
                    EndDate TEXT NULL,
                    Venue TEXT NOT NULL DEFAULT '',
                    Status TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (TournamentId) REFERENCES ManagedTournaments (Id) ON DELETE CASCADE,
                    UNIQUE (TournamentId, Name)
                );
                CREATE TABLE IF NOT EXISTS ManagedLeagueMembers (
                    TournamentId TEXT NOT NULL,
                    UserId TEXT NOT NULL,
                    Role TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    PRIMARY KEY (TournamentId, UserId),
                    FOREIGN KEY (TournamentId) REFERENCES ManagedTournaments (Id) ON DELETE CASCADE,
                    FOREIGN KEY (UserId) REFERENCES AspNetUsers (Id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS ManagedAuctionEvents (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TournamentId TEXT NOT NULL,
                    SeasonId TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    ScheduledAt TEXT NOT NULL,
                    Venue TEXT NOT NULL DEFAULT '',
                    Description TEXT NOT NULL DEFAULT '',
                    Status TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (TournamentId) REFERENCES ManagedTournaments (Id) ON DELETE CASCADE,
                    FOREIGN KEY (SeasonId) REFERENCES ManagedSeasons (Id) ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS IX_ManagedAuctionEvents_SeasonId_ScheduledAt
                    ON ManagedAuctionEvents (SeasonId, ScheduledAt);
                CREATE TABLE IF NOT EXISTS ManagedTeams (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TournamentId TEXT NOT NULL,
                    SeasonId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                    Name TEXT NOT NULL,
                    ShortName TEXT NOT NULL,
                    DisplayName TEXT NOT NULL DEFAULT '',
                    Captain TEXT NOT NULL,
                    City TEXT NOT NULL DEFAULT '',
                    LogoDataUrl TEXT NOT NULL DEFAULT '',
                    Purse INTEGER NOT NULL,
                    InitialPurse INTEGER NOT NULL,
                    Color TEXT NOT NULL,
                    FOREIGN KEY (TournamentId) REFERENCES ManagedTournaments (Id) ON DELETE CASCADE,
                    FOREIGN KEY (SeasonId) REFERENCES ManagedSeasons (Id) ON DELETE CASCADE,
                    UNIQUE (TournamentId, ShortName)
                );
                CREATE TABLE IF NOT EXISTS ManagedPlayers (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TournamentId TEXT NOT NULL,
                    SeasonId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                    FullName TEXT NOT NULL,
                    City TEXT NOT NULL,
                    Email TEXT NOT NULL DEFAULT '',
                    Nationality TEXT NOT NULL DEFAULT '',
                    DateOfBirth TEXT NULL,
                    PhotoDataUrl TEXT NOT NULL DEFAULT '',
                    Role TEXT NOT NULL,
                    BasePrice INTEGER NOT NULL,
                    Status TEXT NOT NULL,
                    TeamId TEXT NULL,
                    SoldPrice INTEGER NULL,
                    FOREIGN KEY (TournamentId) REFERENCES ManagedTournaments (Id) ON DELETE CASCADE,
                    FOREIGN KEY (SeasonId) REFERENCES ManagedSeasons (Id) ON DELETE CASCADE,
                    FOREIGN KEY (TeamId) REFERENCES ManagedTeams (Id) ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS IX_ManagedPlayers_TournamentId_FullName
                    ON ManagedPlayers (TournamentId, FullName);
                CREATE TABLE IF NOT EXISTS ManagedTeamMembers (
                    TeamId TEXT NOT NULL,
                    UserId TEXT NOT NULL,
                    PRIMARY KEY (TeamId, UserId),
                    FOREIGN KEY (TeamId) REFERENCES ManagedTeams (Id) ON DELETE CASCADE,
                    FOREIGN KEY (UserId) REFERENCES AspNetUsers (Id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS AuctionBids (
                    Id TEXT NOT NULL PRIMARY KEY,
                    AuctionId TEXT NOT NULL,
                    PlayerId TEXT NULL,
                    TeamId TEXT NOT NULL,
                    UserId TEXT NOT NULL,
                    ClientRequestId TEXT NOT NULL,
                    Amount INTEGER NOT NULL,
                    SequenceNumber INTEGER NOT NULL,
                    Accepted INTEGER NOT NULL,
                    RejectionReason TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (AuctionId) REFERENCES ManagedAuctionEvents (Id) ON DELETE CASCADE,
                    FOREIGN KEY (PlayerId) REFERENCES ManagedPlayers (Id) ON DELETE RESTRICT,
                    FOREIGN KEY (TeamId) REFERENCES ManagedTeams (Id) ON DELETE RESTRICT,
                    FOREIGN KEY (UserId) REFERENCES AspNetUsers (Id) ON DELETE RESTRICT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_AuctionBids_AuctionId_ClientRequestId
                    ON AuctionBids (AuctionId, ClientRequestId);
                CREATE INDEX IF NOT EXISTS IX_AuctionBids_AuctionId_PlayerId_SequenceNumber
                    ON AuctionBids (AuctionId, PlayerId, SequenceNumber);
                CREATE TABLE IF NOT EXISTS AuctionPurchases (
                    Id TEXT NOT NULL PRIMARY KEY,
                    AuctionId TEXT NOT NULL,
                    PlayerId TEXT NOT NULL,
                    TeamId TEXT NOT NULL,
                    Price INTEGER NOT NULL,
                    Round INTEGER NOT NULL,
                    PurchasedAt TEXT NOT NULL,
                    IsReversed INTEGER NOT NULL DEFAULT 0,
                    ReversedAt TEXT NULL,
                    CorrectionReason TEXT NULL,
                    FOREIGN KEY (AuctionId) REFERENCES ManagedAuctionEvents (Id) ON DELETE CASCADE,
                    FOREIGN KEY (PlayerId) REFERENCES ManagedPlayers (Id) ON DELETE RESTRICT,
                    FOREIGN KEY (TeamId) REFERENCES ManagedTeams (Id) ON DELETE RESTRICT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_AuctionPurchases_PlayerId_Active
                    ON AuctionPurchases (PlayerId) WHERE IsReversed = 0;
                CREATE TABLE IF NOT EXISTS AuctionAuditEvents (
                    Id TEXT NOT NULL PRIMARY KEY,
                    AuctionId TEXT NOT NULL,
                    ActorUserId TEXT NULL,
                    EventSequence INTEGER NOT NULL,
                    EventType TEXT NOT NULL,
                    PlayerId TEXT NULL,
                    TeamId TEXT NULL,
                    Amount INTEGER NULL,
                    Details TEXT NOT NULL DEFAULT '',
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (AuctionId) REFERENCES ManagedAuctionEvents (Id) ON DELETE CASCADE,
                    FOREIGN KEY (ActorUserId) REFERENCES AspNetUsers (Id) ON DELETE SET NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_AuctionAuditEvents_AuctionId_EventSequence
                    ON AuctionAuditEvents (AuctionId, EventSequence);
                """,
                cancellationToken);
            var hasDefaultPurse = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM pragma_table_info('ManagedTournaments') WHERE name = 'DefaultTeamPurse'")
                .SingleAsync(cancellationToken);
            if (hasDefaultPurse == 0)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE ManagedTournaments ADD COLUMN DefaultTeamPurse INTEGER NOT NULL DEFAULT 100000",
                    cancellationToken);
            }
            await AddSqliteColumnIfMissingAsync(db, "ManagedTournaments", "CurrencyCode", "TEXT NOT NULL DEFAULT 'INR'", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedTournaments", "MaximumTeams", "INTEGER NULL", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedTournaments", "MaximumPlayers", "INTEGER NULL", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedTeams", "DisplayName", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedTeams", "SeasonId", "TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedTeams", "City", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedTeams", "LogoDataUrl", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedPlayers", "Email", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedPlayers", "Nationality", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedPlayers", "DateOfBirth", "TEXT NULL", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedPlayers", "PhotoDataUrl", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedPlayers", "SeasonId", "TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "CurrentPlayerId", "TEXT NULL", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "LeadingTeamId", "TEXT NULL", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "CurrentBidAmount", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "BidIncrement", "INTEGER NOT NULL DEFAULT 2000", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "BidDurationSeconds", "INTEGER NOT NULL DEFAULT 30", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "BidExtensionSeconds", "INTEGER NOT NULL DEFAULT 5", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "BidDeadline", "TEXT NULL", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "PausedRemainingMilliseconds", "INTEGER NULL", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "MaximumSquadSize", "INTEGER NOT NULL DEFAULT 25", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "MinimumSquadSize", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "MinimumSquadReservePrice", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "CurrentRound", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "MaximumRounds", "INTEGER NOT NULL DEFAULT 2", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "BidSequence", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "EventSequence", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await AddSqliteColumnIfMissingAsync(db, "ManagedAuctionEvents", "Revision", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureInitialSeasonsAsync(db, cancellationToken);
            await EnsureSeasonScopedTeamCodesAsync(db, cancellationToken);
            return;
        }

        if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'dbo.ManagedTournaments', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.ManagedTournaments (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_ManagedTournaments PRIMARY KEY,
                        OwnerId uniqueidentifier NOT NULL,
                        Name nvarchar(160) NOT NULL,
                        Season nvarchar(40) NOT NULL,
                        Venue nvarchar(160) NOT NULL,
                        DefaultTeamPurse bigint NOT NULL CONSTRAINT DF_ManagedTournaments_DefaultTeamPurse DEFAULT 100000,
                        CurrencyCode nvarchar(3) NOT NULL CONSTRAINT DF_ManagedTournaments_CurrencyCode DEFAULT 'INR',
                        MaximumTeams int NULL,
                        MaximumPlayers int NULL,
                        Status nvarchar(24) NOT NULL,
                        CreatedAt datetimeoffset NOT NULL,
                        CONSTRAINT FK_ManagedTournaments_AspNetUsers_OwnerId
                            FOREIGN KEY (OwnerId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
                    );
                    CREATE INDEX IX_ManagedTournaments_OwnerId_CreatedAt
                        ON dbo.ManagedTournaments (OwnerId, CreatedAt);
                END;
                IF COL_LENGTH(N'dbo.ManagedTournaments', N'DefaultTeamPurse') IS NULL
                    ALTER TABLE dbo.ManagedTournaments ADD DefaultTeamPurse bigint NOT NULL
                        CONSTRAINT DF_ManagedTournaments_DefaultTeamPurse DEFAULT 100000;
                IF COL_LENGTH(N'dbo.ManagedTournaments', N'CurrencyCode') IS NULL
                    ALTER TABLE dbo.ManagedTournaments ADD CurrencyCode nvarchar(3) NOT NULL
                        CONSTRAINT DF_ManagedTournaments_CurrencyCode DEFAULT 'INR';
                IF COL_LENGTH(N'dbo.ManagedTournaments', N'MaximumTeams') IS NULL
                    ALTER TABLE dbo.ManagedTournaments ADD MaximumTeams int NULL;
                IF COL_LENGTH(N'dbo.ManagedTournaments', N'MaximumPlayers') IS NULL
                    ALTER TABLE dbo.ManagedTournaments ADD MaximumPlayers int NULL;
                IF OBJECT_ID(N'dbo.ManagedSeasons', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.ManagedSeasons (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_ManagedSeasons PRIMARY KEY,
                        TournamentId uniqueidentifier NOT NULL,
                        Name nvarchar(80) NOT NULL,
                        StartDate date NULL,
                        EndDate date NULL,
                        Venue nvarchar(160) NOT NULL CONSTRAINT DF_ManagedSeasons_Venue DEFAULT '',
                        Status nvarchar(24) NOT NULL,
                        CreatedAt datetimeoffset NOT NULL,
                        CONSTRAINT FK_ManagedSeasons_ManagedTournaments_TournamentId
                            FOREIGN KEY (TournamentId) REFERENCES dbo.ManagedTournaments(Id) ON DELETE CASCADE,
                        CONSTRAINT UQ_ManagedSeasons_TournamentId_Name UNIQUE (TournamentId, Name)
                    );
                END;
                IF OBJECT_ID(N'dbo.ManagedAuctionEvents', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.ManagedAuctionEvents (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_ManagedAuctionEvents PRIMARY KEY,
                        TournamentId uniqueidentifier NOT NULL,
                        SeasonId uniqueidentifier NOT NULL,
                        Name nvarchar(120) NOT NULL,
                        ScheduledAt datetimeoffset NOT NULL,
                        Venue nvarchar(160) NOT NULL CONSTRAINT DF_ManagedAuctionEvents_Venue DEFAULT '',
                        Description nvarchar(1000) NOT NULL CONSTRAINT DF_ManagedAuctionEvents_Description DEFAULT '',
                        Status nvarchar(24) NOT NULL,
                        CreatedAt datetimeoffset NOT NULL,
                        CONSTRAINT FK_ManagedAuctionEvents_ManagedTournaments_TournamentId
                            FOREIGN KEY (TournamentId) REFERENCES dbo.ManagedTournaments(Id) ON DELETE CASCADE,
                        CONSTRAINT FK_ManagedAuctionEvents_ManagedSeasons_SeasonId
                            FOREIGN KEY (SeasonId) REFERENCES dbo.ManagedSeasons(Id) ON DELETE NO ACTION
                    );
                    CREATE INDEX IX_ManagedAuctionEvents_SeasonId_ScheduledAt
                        ON dbo.ManagedAuctionEvents (SeasonId, ScheduledAt);
                END;
                IF OBJECT_ID(N'dbo.ManagedLeagueMembers', N'U') IS NULL
                BEGIN
                        CREATE TABLE dbo.ManagedLeagueMembers (
                            TournamentId uniqueidentifier NOT NULL,
                            UserId uniqueidentifier NOT NULL,
                            Role nvarchar(24) NOT NULL,
                            CreatedAt datetimeoffset NOT NULL,
                            CONSTRAINT PK_ManagedLeagueMembers PRIMARY KEY (TournamentId, UserId),
                            CONSTRAINT FK_ManagedLeagueMembers_ManagedTournaments_TournamentId
                                FOREIGN KEY (TournamentId) REFERENCES dbo.ManagedTournaments(Id) ON DELETE CASCADE,
                            CONSTRAINT FK_ManagedLeagueMembers_AspNetUsers_UserId
                                FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
                        );
                END;
                IF OBJECT_ID(N'dbo.ManagedTeams', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.ManagedTeams (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_ManagedTeams PRIMARY KEY,
                        TournamentId uniqueidentifier NOT NULL,
                        SeasonId uniqueidentifier NOT NULL CONSTRAINT DF_ManagedTeams_SeasonId
                            DEFAULT '00000000-0000-0000-0000-000000000000',
                        Name nvarchar(100) NOT NULL,
                        ShortName nvarchar(8) NOT NULL,
                        DisplayName nvarchar(100) NOT NULL CONSTRAINT DF_ManagedTeams_DisplayName DEFAULT '',
                        Captain nvarchar(100) NOT NULL,
                        City nvarchar(120) NOT NULL CONSTRAINT DF_ManagedTeams_City DEFAULT '',
                        LogoDataUrl nvarchar(max) NOT NULL CONSTRAINT DF_ManagedTeams_LogoDataUrl DEFAULT '',
                        Purse bigint NOT NULL,
                        InitialPurse bigint NOT NULL,
                        Color nvarchar(16) NOT NULL,
                        CONSTRAINT FK_ManagedTeams_ManagedTournaments_TournamentId
                            FOREIGN KEY (TournamentId) REFERENCES dbo.ManagedTournaments(Id) ON DELETE CASCADE,
                        CONSTRAINT FK_ManagedTeams_ManagedSeasons_SeasonId
                            FOREIGN KEY (SeasonId) REFERENCES dbo.ManagedSeasons(Id) ON DELETE CASCADE,
                        CONSTRAINT UQ_ManagedTeams_TournamentId_ShortName UNIQUE (TournamentId, ShortName)
                    );
                END;
                IF OBJECT_ID(N'dbo.ManagedPlayers', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.ManagedPlayers (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_ManagedPlayers PRIMARY KEY,
                        TournamentId uniqueidentifier NOT NULL,
                        SeasonId uniqueidentifier NOT NULL CONSTRAINT DF_ManagedPlayers_SeasonId
                            DEFAULT '00000000-0000-0000-0000-000000000000',
                        FullName nvarchar(120) NOT NULL,
                        City nvarchar(120) NOT NULL,
                        Email nvarchar(254) NOT NULL CONSTRAINT DF_ManagedPlayers_Email DEFAULT '',
                        Nationality nvarchar(80) NOT NULL CONSTRAINT DF_ManagedPlayers_Nationality DEFAULT '',
                        DateOfBirth nvarchar(10) NULL,
                        PhotoDataUrl nvarchar(max) NOT NULL CONSTRAINT DF_ManagedPlayers_PhotoDataUrl DEFAULT '',
                        Role nvarchar(32) NOT NULL,
                        BasePrice bigint NOT NULL,
                        Status nvarchar(24) NOT NULL,
                        TeamId uniqueidentifier NULL,
                        SoldPrice bigint NULL,
                        CONSTRAINT FK_ManagedPlayers_ManagedTournaments_TournamentId
                            FOREIGN KEY (TournamentId) REFERENCES dbo.ManagedTournaments(Id) ON DELETE CASCADE,
                        CONSTRAINT FK_ManagedPlayers_ManagedSeasons_SeasonId
                            FOREIGN KEY (SeasonId) REFERENCES dbo.ManagedSeasons(Id) ON DELETE CASCADE,
                        CONSTRAINT FK_ManagedPlayers_ManagedTeams_TeamId
                            FOREIGN KEY (TeamId) REFERENCES dbo.ManagedTeams(Id) ON DELETE SET NULL
                    );
                    CREATE INDEX IX_ManagedPlayers_TournamentId_FullName
                        ON dbo.ManagedPlayers (TournamentId, FullName);
                END;
                IF COL_LENGTH(N'dbo.ManagedTeams', N'SeasonId') IS NULL
                    ALTER TABLE dbo.ManagedTeams ADD SeasonId uniqueidentifier NOT NULL
                        CONSTRAINT DF_ManagedTeams_SeasonId DEFAULT '00000000-0000-0000-0000-000000000000';
                IF COL_LENGTH(N'dbo.ManagedPlayers', N'SeasonId') IS NULL
                    ALTER TABLE dbo.ManagedPlayers ADD SeasonId uniqueidentifier NOT NULL
                        CONSTRAINT DF_ManagedPlayers_SeasonId DEFAULT '00000000-0000-0000-0000-000000000000';
                IF COL_LENGTH(N'dbo.ManagedTeams', N'DisplayName') IS NULL
                    ALTER TABLE dbo.ManagedTeams ADD DisplayName nvarchar(100) NOT NULL
                        CONSTRAINT DF_ManagedTeams_DisplayName DEFAULT '';
                IF COL_LENGTH(N'dbo.ManagedTeams', N'City') IS NULL
                    ALTER TABLE dbo.ManagedTeams ADD City nvarchar(120) NOT NULL
                        CONSTRAINT DF_ManagedTeams_City DEFAULT '';
                IF COL_LENGTH(N'dbo.ManagedTeams', N'LogoDataUrl') IS NULL
                    ALTER TABLE dbo.ManagedTeams ADD LogoDataUrl nvarchar(max) NOT NULL
                        CONSTRAINT DF_ManagedTeams_LogoDataUrl DEFAULT '';
                IF COL_LENGTH(N'dbo.ManagedPlayers', N'Email') IS NULL
                    ALTER TABLE dbo.ManagedPlayers ADD Email nvarchar(254) NOT NULL
                        CONSTRAINT DF_ManagedPlayers_Email DEFAULT '';
                IF COL_LENGTH(N'dbo.ManagedPlayers', N'Nationality') IS NULL
                    ALTER TABLE dbo.ManagedPlayers ADD Nationality nvarchar(80) NOT NULL
                        CONSTRAINT DF_ManagedPlayers_Nationality DEFAULT '';
                IF COL_LENGTH(N'dbo.ManagedPlayers', N'DateOfBirth') IS NULL
                    ALTER TABLE dbo.ManagedPlayers ADD DateOfBirth nvarchar(10) NULL;
                IF COL_LENGTH(N'dbo.ManagedPlayers', N'PhotoDataUrl') IS NULL
                    ALTER TABLE dbo.ManagedPlayers ADD PhotoDataUrl nvarchar(max) NOT NULL
                        CONSTRAINT DF_ManagedPlayers_PhotoDataUrl DEFAULT '';
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'CurrentPlayerId') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD CurrentPlayerId uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'LeadingTeamId') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD LeadingTeamId uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'CurrentBidAmount') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD CurrentBidAmount bigint NOT NULL DEFAULT 0;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'BidIncrement') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD BidIncrement bigint NOT NULL DEFAULT 2000;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'BidDurationSeconds') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD BidDurationSeconds int NOT NULL DEFAULT 30;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'BidExtensionSeconds') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD BidExtensionSeconds int NOT NULL DEFAULT 5;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'BidDeadline') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD BidDeadline datetimeoffset NULL;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'PausedRemainingMilliseconds') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD PausedRemainingMilliseconds int NULL;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'MaximumSquadSize') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD MaximumSquadSize int NOT NULL DEFAULT 25;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'MinimumSquadSize') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD MinimumSquadSize int NOT NULL DEFAULT 0;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'MinimumSquadReservePrice') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD MinimumSquadReservePrice bigint NOT NULL DEFAULT 0;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'CurrentRound') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD CurrentRound int NOT NULL DEFAULT 1;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'MaximumRounds') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD MaximumRounds int NOT NULL DEFAULT 2;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'BidSequence') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD BidSequence int NOT NULL DEFAULT 0;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'EventSequence') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD EventSequence int NOT NULL DEFAULT 0;
                IF COL_LENGTH(N'dbo.ManagedAuctionEvents', N'Revision') IS NULL
                    ALTER TABLE dbo.ManagedAuctionEvents ADD Revision bigint NOT NULL DEFAULT 0;
                IF OBJECT_ID(N'dbo.ManagedTeamMembers', N'U') IS NULL
                    CREATE TABLE dbo.ManagedTeamMembers (
                        TeamId uniqueidentifier NOT NULL,
                        UserId uniqueidentifier NOT NULL,
                        CONSTRAINT PK_ManagedTeamMembers PRIMARY KEY (TeamId, UserId),
                        CONSTRAINT FK_ManagedTeamMembers_ManagedTeams_TeamId
                            FOREIGN KEY (TeamId) REFERENCES dbo.ManagedTeams(Id) ON DELETE CASCADE,
                        CONSTRAINT FK_ManagedTeamMembers_AspNetUsers_UserId
                            FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
                    );
                IF OBJECT_ID(N'dbo.AuctionBids', N'U') IS NULL
                    CREATE TABLE dbo.AuctionBids (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_AuctionBids PRIMARY KEY,
                        AuctionId uniqueidentifier NOT NULL,
                        PlayerId uniqueidentifier NULL,
                        TeamId uniqueidentifier NOT NULL,
                        UserId uniqueidentifier NOT NULL,
                        ClientRequestId uniqueidentifier NOT NULL,
                        Amount bigint NOT NULL,
                        SequenceNumber int NOT NULL,
                        Accepted bit NOT NULL,
                        RejectionReason nvarchar(240) NULL,
                        CreatedAt datetimeoffset NOT NULL,
                        CONSTRAINT FK_AuctionBids_ManagedAuctionEvents_AuctionId
                            FOREIGN KEY (AuctionId) REFERENCES dbo.ManagedAuctionEvents(Id) ON DELETE CASCADE,
                        CONSTRAINT FK_AuctionBids_ManagedPlayers_PlayerId
                            FOREIGN KEY (PlayerId) REFERENCES dbo.ManagedPlayers(Id),
                        CONSTRAINT FK_AuctionBids_ManagedTeams_TeamId
                            FOREIGN KEY (TeamId) REFERENCES dbo.ManagedTeams(Id),
                        CONSTRAINT FK_AuctionBids_AspNetUsers_UserId
                            FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id)
                    );
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuctionBids')
                    AND name = N'IX_AuctionBids_AuctionId_ClientRequestId')
                    CREATE UNIQUE INDEX IX_AuctionBids_AuctionId_ClientRequestId
                        ON dbo.AuctionBids (AuctionId, ClientRequestId);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuctionBids')
                    AND name = N'IX_AuctionBids_AuctionId_PlayerId_SequenceNumber')
                    CREATE INDEX IX_AuctionBids_AuctionId_PlayerId_SequenceNumber
                        ON dbo.AuctionBids (AuctionId, PlayerId, SequenceNumber);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuctionBids')
                    AND name = N'IX_AuctionBids_AuctionId_PlayerId_AcceptedSequence')
                    CREATE UNIQUE INDEX IX_AuctionBids_AuctionId_PlayerId_AcceptedSequence
                        ON dbo.AuctionBids (AuctionId, PlayerId, SequenceNumber) WHERE Accepted = 1;
                IF OBJECT_ID(N'dbo.AuctionPurchases', N'U') IS NULL
                    CREATE TABLE dbo.AuctionPurchases (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_AuctionPurchases PRIMARY KEY,
                        AuctionId uniqueidentifier NOT NULL,
                        PlayerId uniqueidentifier NOT NULL,
                        TeamId uniqueidentifier NOT NULL,
                        Price bigint NOT NULL,
                        Round int NOT NULL,
                        PurchasedAt datetimeoffset NOT NULL,
                        IsReversed bit NOT NULL DEFAULT 0,
                        ReversedAt datetimeoffset NULL,
                        CorrectionReason nvarchar(500) NULL,
                        CONSTRAINT FK_AuctionPurchases_ManagedAuctionEvents_AuctionId
                            FOREIGN KEY (AuctionId) REFERENCES dbo.ManagedAuctionEvents(Id) ON DELETE CASCADE,
                        CONSTRAINT FK_AuctionPurchases_ManagedPlayers_PlayerId
                            FOREIGN KEY (PlayerId) REFERENCES dbo.ManagedPlayers(Id),
                        CONSTRAINT FK_AuctionPurchases_ManagedTeams_TeamId
                            FOREIGN KEY (TeamId) REFERENCES dbo.ManagedTeams(Id)
                    );
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuctionPurchases')
                    AND name = N'IX_AuctionPurchases_PlayerId_Active')
                    CREATE UNIQUE INDEX IX_AuctionPurchases_PlayerId_Active
                        ON dbo.AuctionPurchases (PlayerId) WHERE IsReversed = 0;
                IF OBJECT_ID(N'dbo.AuctionAuditEvents', N'U') IS NULL
                    CREATE TABLE dbo.AuctionAuditEvents (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_AuctionAuditEvents PRIMARY KEY,
                        AuctionId uniqueidentifier NOT NULL,
                        ActorUserId uniqueidentifier NULL,
                        EventSequence int NOT NULL,
                        EventType nvarchar(48) NOT NULL,
                        PlayerId uniqueidentifier NULL,
                        TeamId uniqueidentifier NULL,
                        Amount bigint NULL,
                        Details nvarchar(2000) NOT NULL DEFAULT '',
                        CreatedAt datetimeoffset NOT NULL,
                        CONSTRAINT FK_AuctionAuditEvents_ManagedAuctionEvents_AuctionId
                            FOREIGN KEY (AuctionId) REFERENCES dbo.ManagedAuctionEvents(Id) ON DELETE CASCADE,
                        CONSTRAINT FK_AuctionAuditEvents_AspNetUsers_ActorUserId
                            FOREIGN KEY (ActorUserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE SET NULL
                    );
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuctionAuditEvents')
                    AND name = N'IX_AuctionAuditEvents_AuctionId_EventSequence')
                    CREATE UNIQUE INDEX IX_AuctionAuditEvents_AuctionId_EventSequence
                        ON dbo.AuctionAuditEvents (AuctionId, EventSequence);
                IF EXISTS (
                    SELECT 1 FROM sys.key_constraints
                    WHERE parent_object_id = OBJECT_ID(N'dbo.ManagedTeams')
                        AND name = N'UQ_ManagedTeams_TournamentId_ShortName'
                )
                    ALTER TABLE dbo.ManagedTeams DROP CONSTRAINT UQ_ManagedTeams_TournamentId_ShortName;
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.ManagedTeams')
                        AND name = N'IX_ManagedTeams_TournamentId_ShortName'
                )
                    DROP INDEX IX_ManagedTeams_TournamentId_ShortName ON dbo.ManagedTeams;
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.ManagedTeams')
                        AND name = N'IX_ManagedTeams_SeasonId_ShortName'
                )
                    CREATE UNIQUE INDEX IX_ManagedTeams_SeasonId_ShortName
                        ON dbo.ManagedTeams (SeasonId, ShortName);
                """,
                cancellationToken);
            await EnsureInitialSeasonsAsync(db, cancellationToken);
            await EnsureSeasonScopedTeamCodesAsync(db, cancellationToken);
            return;
        }

        throw new InvalidOperationException("The configured database provider is not supported.");
    }

    private static async Task EnsureSeasonScopedTeamCodesAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlite())
        {
            return;
        }

        var tableSql = await db.Database.SqlQueryRaw<string?>(
                "SELECT sql AS Value FROM sqlite_master WHERE type = 'table' AND name = 'ManagedTeams'")
            .SingleOrDefaultAsync(cancellationToken);
        if (tableSql?.Contains("UNIQUE (TournamentId, ShortName)", StringComparison.OrdinalIgnoreCase) == true)
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                PRAGMA foreign_keys = OFF;
                CREATE TABLE ManagedTeams_SeasonIndexMigration (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TournamentId TEXT NOT NULL,
                    SeasonId TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    ShortName TEXT NOT NULL,
                    DisplayName TEXT NOT NULL DEFAULT '',
                    Captain TEXT NOT NULL,
                    City TEXT NOT NULL DEFAULT '',
                    LogoDataUrl TEXT NOT NULL DEFAULT '',
                    Purse INTEGER NOT NULL,
                    InitialPurse INTEGER NOT NULL,
                    Color TEXT NOT NULL,
                    FOREIGN KEY (TournamentId) REFERENCES ManagedTournaments (Id) ON DELETE CASCADE,
                    FOREIGN KEY (SeasonId) REFERENCES ManagedSeasons (Id) ON DELETE CASCADE
                );
                INSERT INTO ManagedTeams_SeasonIndexMigration
                    (Id, TournamentId, SeasonId, Name, ShortName, DisplayName, Captain, City, LogoDataUrl, Purse, InitialPurse, Color)
                SELECT Id, TournamentId, SeasonId, Name, ShortName, DisplayName, Captain, City, LogoDataUrl, Purse, InitialPurse, Color
                FROM ManagedTeams;
                DROP TABLE ManagedTeams;
                ALTER TABLE ManagedTeams_SeasonIndexMigration RENAME TO ManagedTeams;
                PRAGMA foreign_keys = ON;
                """,
                cancellationToken);
        }

        await db.Database.ExecuteSqlRawAsync(
            """
            DROP INDEX IF EXISTS IX_ManagedTeams_TournamentId_ShortName;
            CREATE UNIQUE INDEX IF NOT EXISTS IX_ManagedTeams_SeasonId_ShortName
                ON ManagedTeams (SeasonId, ShortName);
            """,
            cancellationToken);
    }

    private static async Task EnsureInitialSeasonsAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var tournaments = await db.Tournaments
            .Include(tournament => tournament.Seasons)
            .Include(tournament => tournament.Teams)
            .Include(tournament => tournament.Players)
            .ToListAsync(cancellationToken);

        foreach (var tournament in tournaments)
        {
            var season = tournament.Seasons.OrderBy(item => item.CreatedAt).FirstOrDefault();
            if (season is null)
            {
                season = new ProCricketAuction.Domain.Entities.TournamentSeason
                {
                    Id = Guid.NewGuid(),
                    TournamentId = tournament.Id,
                    Name = string.IsNullOrWhiteSpace(tournament.Season) ? "Season 1" : tournament.Season.Trim(),
                    Venue = tournament.Venue,
                    CreatedAt = tournament.CreatedAt
                };
                db.TournamentSeasons.Add(season);
            }

            foreach (var team in tournament.Teams.Where(item => item.SeasonId == Guid.Empty))
            {
                team.SeasonId = season.Id;
            }

            foreach (var player in tournament.Players.Where(item => item.SeasonId == Guid.Empty))
            {
                player.SeasonId = season.Id;
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task AddSqliteColumnIfMissingAsync(
        ApplicationDbContext db,
        string table,
        string column,
        string definition,
        CancellationToken cancellationToken)
    {
        var exists = await db.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS Value FROM pragma_table_info({table}) WHERE name = {column}")
            .SingleAsync(cancellationToken);
        if (exists == 0)
        {
            var sql = (table, column, definition) switch
            {
                ("ManagedTeams", "DisplayName", "TEXT NOT NULL DEFAULT ''") =>
                    "ALTER TABLE ManagedTeams ADD COLUMN DisplayName TEXT NOT NULL DEFAULT ''",
                ("ManagedTeams", "SeasonId", "TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'") =>
                    "ALTER TABLE ManagedTeams ADD COLUMN SeasonId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'",
                ("ManagedTeams", "City", "TEXT NOT NULL DEFAULT ''") =>
                    "ALTER TABLE ManagedTeams ADD COLUMN City TEXT NOT NULL DEFAULT ''",
                ("ManagedTeams", "LogoDataUrl", "TEXT NOT NULL DEFAULT ''") =>
                    "ALTER TABLE ManagedTeams ADD COLUMN LogoDataUrl TEXT NOT NULL DEFAULT ''",
                ("ManagedPlayers", "Email", "TEXT NOT NULL DEFAULT ''") =>
                    "ALTER TABLE ManagedPlayers ADD COLUMN Email TEXT NOT NULL DEFAULT ''",
                ("ManagedPlayers", "Nationality", "TEXT NOT NULL DEFAULT ''") =>
                    "ALTER TABLE ManagedPlayers ADD COLUMN Nationality TEXT NOT NULL DEFAULT ''",
                ("ManagedPlayers", "DateOfBirth", "TEXT NULL") =>
                    "ALTER TABLE ManagedPlayers ADD COLUMN DateOfBirth TEXT NULL",
                ("ManagedPlayers", "PhotoDataUrl", "TEXT NOT NULL DEFAULT ''") =>
                    "ALTER TABLE ManagedPlayers ADD COLUMN PhotoDataUrl TEXT NOT NULL DEFAULT ''",
                ("ManagedPlayers", "SeasonId", "TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'") =>
                    "ALTER TABLE ManagedPlayers ADD COLUMN SeasonId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'",
                ("ManagedTournaments", "CurrencyCode", "TEXT NOT NULL DEFAULT 'INR'") =>
                    "ALTER TABLE ManagedTournaments ADD COLUMN CurrencyCode TEXT NOT NULL DEFAULT 'INR'",
                ("ManagedTournaments", "MaximumTeams", "INTEGER NULL") =>
                    "ALTER TABLE ManagedTournaments ADD COLUMN MaximumTeams INTEGER NULL",
                ("ManagedTournaments", "MaximumPlayers", "INTEGER NULL") =>
                    "ALTER TABLE ManagedTournaments ADD COLUMN MaximumPlayers INTEGER NULL",
                ("ManagedAuctionEvents", "CurrentPlayerId", "TEXT NULL") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN CurrentPlayerId TEXT NULL",
                ("ManagedAuctionEvents", "LeadingTeamId", "TEXT NULL") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN LeadingTeamId TEXT NULL",
                ("ManagedAuctionEvents", "CurrentBidAmount", "INTEGER NOT NULL DEFAULT 0") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN CurrentBidAmount INTEGER NOT NULL DEFAULT 0",
                ("ManagedAuctionEvents", "BidIncrement", "INTEGER NOT NULL DEFAULT 2000") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN BidIncrement INTEGER NOT NULL DEFAULT 2000",
                ("ManagedAuctionEvents", "BidDurationSeconds", "INTEGER NOT NULL DEFAULT 30") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN BidDurationSeconds INTEGER NOT NULL DEFAULT 30",
                ("ManagedAuctionEvents", "BidExtensionSeconds", "INTEGER NOT NULL DEFAULT 5") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN BidExtensionSeconds INTEGER NOT NULL DEFAULT 5",
                ("ManagedAuctionEvents", "BidDeadline", "TEXT NULL") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN BidDeadline TEXT NULL",
                ("ManagedAuctionEvents", "PausedRemainingMilliseconds", "INTEGER NULL") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN PausedRemainingMilliseconds INTEGER NULL",
                ("ManagedAuctionEvents", "MaximumSquadSize", "INTEGER NOT NULL DEFAULT 25") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN MaximumSquadSize INTEGER NOT NULL DEFAULT 25",
                ("ManagedAuctionEvents", "MinimumSquadSize", "INTEGER NOT NULL DEFAULT 0") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN MinimumSquadSize INTEGER NOT NULL DEFAULT 0",
                ("ManagedAuctionEvents", "MinimumSquadReservePrice", "INTEGER NOT NULL DEFAULT 0") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN MinimumSquadReservePrice INTEGER NOT NULL DEFAULT 0",
                ("ManagedAuctionEvents", "CurrentRound", "INTEGER NOT NULL DEFAULT 1") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN CurrentRound INTEGER NOT NULL DEFAULT 1",
                ("ManagedAuctionEvents", "MaximumRounds", "INTEGER NOT NULL DEFAULT 2") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN MaximumRounds INTEGER NOT NULL DEFAULT 2",
                ("ManagedAuctionEvents", "BidSequence", "INTEGER NOT NULL DEFAULT 0") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN BidSequence INTEGER NOT NULL DEFAULT 0",
                ("ManagedAuctionEvents", "EventSequence", "INTEGER NOT NULL DEFAULT 0") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN EventSequence INTEGER NOT NULL DEFAULT 0",
                ("ManagedAuctionEvents", "Revision", "INTEGER NOT NULL DEFAULT 0") =>
                    "ALTER TABLE ManagedAuctionEvents ADD COLUMN Revision INTEGER NOT NULL DEFAULT 0",
                _ => throw new InvalidOperationException($"Unsupported SQLite schema change: {table}.{column}.")
            };
            await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }
}
