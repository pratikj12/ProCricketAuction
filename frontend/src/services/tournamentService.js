import apiClient from './apiClient.js';

const tournamentService = {
  async list() {
    const { data } = await apiClient.get('/tournaments');
    return data;
  },
  async create(tournament) {
    const { data } = await apiClient.post('/tournaments', tournament);
    return data;
  },
  async import(workspace) {
    const { data } = await apiClient.post('/tournaments/import', workspace);
    return data;
  },
  async update(id, tournament) {
    const { data } = await apiClient.put(`/tournaments/${id}`, tournament);
    return data;
  },
  async createSeason(tournamentId, season) {
    const { data } = await apiClient.post(`/tournaments/${tournamentId}/seasons`, season);
    return data;
  },
  async copyRoster(tournamentId, seasonId, roster) {
    const { data } = await apiClient.post(`/tournaments/${tournamentId}/seasons/${seasonId}/copy-roster`, roster);
    return data;
  },
  async updateSeason(tournamentId, seasonId, season) {
    const { data } = await apiClient.put(`/tournaments/${tournamentId}/seasons/${seasonId}`, season);
    return data;
  },
  removeSeason: (tournamentId, seasonId) => apiClient.delete(`/tournaments/${tournamentId}/seasons/${seasonId}`),
  async createAuctionEvent(tournamentId, event) {
    const { data } = await apiClient.post(`/tournaments/${tournamentId}/auction-events`, event);
    return data;
  },
  async updateAuctionEvent(tournamentId, eventId, event) {
    const { data } = await apiClient.put(`/tournaments/${tournamentId}/auction-events/${eventId}`, event);
    return data;
  },
  removeAuctionEvent: (tournamentId, eventId) => apiClient.delete(`/tournaments/${tournamentId}/auction-events/${eventId}`),
  async listMembers(tournamentId) {
    const { data } = await apiClient.get(`/tournaments/${tournamentId}/members`);
    return data;
  },
  async addMember(tournamentId, member) {
    const { data } = await apiClient.post(`/tournaments/${tournamentId}/members`, member);
    return data;
  },
  async updateMemberRole(tournamentId, userId, role, teamIds = []) {
    const { data } = await apiClient.put(`/tournaments/${tournamentId}/members/${userId}`, { role, teamIds });
    return data;
  },
  removeMember: (tournamentId, userId) => apiClient.delete(`/tournaments/${tournamentId}/members/${userId}`),
  remove: (id) => apiClient.delete(`/tournaments/${id}`)
};

export default tournamentService;
