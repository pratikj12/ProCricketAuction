import axios from 'axios';

let csrfToken = null;

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api/v1',
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' }
});

apiClient.interceptors.request.use((config) => {
  if (csrfToken && ['post', 'put', 'patch', 'delete'].includes(config.method?.toLowerCase() ?? '')) {
    config.headers['X-CSRF-TOKEN'] = csrfToken;
  }
  return config;
});

export function setCsrfToken(token) {
  csrfToken = token;
}

export default apiClient;
