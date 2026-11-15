import apiClient from './apiClient.js';
import { setCsrfToken } from './apiClient.js';

const authService = {
  async initialize() {
    const { data } = await apiClient.get('/auth/csrf');
    setCsrfToken(data.requestToken);
  },
  async currentUser() {
    const { data } = await apiClient.get('/auth/me');
    return data;
  },
  async login(credentials) {
    const { data } = await apiClient.post('/auth/login', credentials);
    return data;
  },
  async register(account) {
    const { data } = await apiClient.post('/auth/register', account);
    return data;
  },
  async updateProfile(profile) {
    const { data } = await apiClient.put('/auth/profile', profile);
    return data;
  },
  async changePassword(passwords) {
    const { data } = await apiClient.post('/auth/change-password', passwords);
    return data;
  },
  logout: () => apiClient.post('/auth/logout')
};

export default authService;
