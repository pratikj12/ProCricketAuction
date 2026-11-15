import apiClient from './apiClient.js';

const playerService = {
  async create(tournamentId, player) {
    const { data } = await apiClient.post(`/tournaments/${tournamentId}/players`, player);
    return data;
  }
};

export default playerService;
