import apiClient from './apiClient.js';

const auctionPath = (auctionId) => `/auctions/${encodeURIComponent(auctionId)}`;

const auctionService = {
  async getState(auctionId) {
    const response = await apiClient.get(`${auctionPath(auctionId)}/state`);
    return response.data;
  },
  async getPublicState(auctionId) {
    const response = await apiClient.get(`/public/auctions/${encodeURIComponent(auctionId)}`);
    return response.data;
  },
  async start(auctionId) {
    const response = await apiClient.post(`${auctionPath(auctionId)}/start`);
    return response.data;
  },
  async pause(auctionId) {
    const response = await apiClient.post(`${auctionPath(auctionId)}/pause`);
    return response.data;
  },
  async resume(auctionId) {
    const response = await apiClient.post(`${auctionPath(auctionId)}/resume`);
    return response.data;
  },
  async presentPlayer(auctionId, playerId) {
    const response = await apiClient.post(`${auctionPath(auctionId)}/players/present`, { playerId });
    return response.data;
  },
  async nextPlayer(auctionId) {
    const response = await apiClient.post(`${auctionPath(auctionId)}/players/next`);
    return response.data;
  },
  async skipPlayer(auctionId) {
    const response = await apiClient.post(`${auctionPath(auctionId)}/players/skip`);
    return response.data;
  },
  async markSold(auctionId, expectedRevision) {
    const response = await apiClient.post(`${auctionPath(auctionId)}/sold`, { expectedRevision });
    return response.data;
  },
  async markUnsold(auctionId) {
    const response = await apiClient.post(`${auctionPath(auctionId)}/unsold`);
    return response.data;
  },
  async startNextRound(auctionId) {
    const response = await apiClient.post(`${auctionPath(auctionId)}/rounds/next`);
    return response.data;
  },
  async placeBid(auctionId, bid) {
    const response = await apiClient.post(`${auctionPath(auctionId)}/bids`, bid);
    return response.data;
  },
  async reverseSale(auctionId, correction) {
    const response = await apiClient.post(`${auctionPath(auctionId)}/corrections/reverse-sale`, correction);
    return response.data;
  }
};

export default auctionService;
