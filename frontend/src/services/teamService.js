import apiClient from './apiClient.js';

const teamService = {
  async create(tournamentId, team) {
    const { data } = await apiClient.post(`/tournaments/${tournamentId}/teams`, team);
    return data;
  },
  remove: (tournamentId, teamId) => apiClient.delete(`/tournaments/${tournamentId}/teams/${teamId}`)
};

export default teamService;
