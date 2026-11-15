using ProCricketAuction.Api.DTOs;

namespace ProCricketAuction.Api.Services;

public interface IWorkspaceService
{
    Task<IReadOnlyList<TournamentWorkspaceResponse>> GetWorkspacesAsync(Guid ownerId, CancellationToken cancellationToken);
    Task<TournamentWorkspaceResponse> ImportWorkspaceAsync(Guid ownerId, ImportWorkspaceRequest request, CancellationToken cancellationToken);
    Task<TournamentResponse> CreateTournamentAsync(Guid ownerId, TournamentRequest request, CancellationToken cancellationToken);
    Task<TournamentResponse?> UpdateTournamentAsync(Guid ownerId, Guid tournamentId, TournamentRequest request, CancellationToken cancellationToken);
    Task<SeasonResponse?> CreateSeasonAsync(Guid ownerId, Guid tournamentId, SeasonRequest request, CancellationToken cancellationToken);
    Task<CopyRosterResponse?> CopyRosterAsync(Guid ownerId, Guid tournamentId, Guid targetSeasonId, CopyRosterRequest request, CancellationToken cancellationToken);
    Task<SeasonResponse?> UpdateSeasonAsync(Guid ownerId, Guid tournamentId, Guid seasonId, SeasonRequest request, CancellationToken cancellationToken);
    Task<bool?> DeleteSeasonAsync(Guid ownerId, Guid tournamentId, Guid seasonId, CancellationToken cancellationToken);
    Task<AuctionEventResponse?> CreateAuctionEventAsync(Guid ownerId, Guid tournamentId, AuctionEventRequest request, CancellationToken cancellationToken);
    Task<AuctionEventResponse?> UpdateAuctionEventAsync(Guid ownerId, Guid tournamentId, Guid eventId, AuctionEventRequest request, CancellationToken cancellationToken);
    Task<bool?> DeleteAuctionEventAsync(Guid ownerId, Guid tournamentId, Guid eventId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeagueMemberResponse>?> GetLeagueMembersAsync(Guid ownerId, Guid tournamentId, CancellationToken cancellationToken);
    Task<LeagueMemberResponse?> AddLeagueMemberAsync(Guid ownerId, Guid tournamentId, AddLeagueMemberRequest request, CancellationToken cancellationToken);
    Task<LeagueMemberResponse?> UpdateLeagueMemberRoleAsync(Guid ownerId, Guid tournamentId, Guid userId, UpdateLeagueMemberRoleRequest request, CancellationToken cancellationToken);
    Task<bool?> RemoveLeagueMemberAsync(Guid ownerId, Guid tournamentId, Guid userId, CancellationToken cancellationToken);
    Task<TeamResponse?> CreateTeamAsync(Guid ownerId, Guid tournamentId, CreateTeamRequest request, CancellationToken cancellationToken);
    Task<bool?> DeleteTeamAsync(Guid ownerId, Guid tournamentId, Guid teamId, CancellationToken cancellationToken);
    Task<PlayerResponse?> CreatePlayerAsync(Guid ownerId, Guid tournamentId, CreatePlayerRequest request, CancellationToken cancellationToken);
    Task<bool?> DeleteTournamentAsync(Guid ownerId, Guid tournamentId, CancellationToken cancellationToken);
}
