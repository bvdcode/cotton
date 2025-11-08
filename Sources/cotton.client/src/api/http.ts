import axios from "axios";
import type {
  AxiosError,
  InternalAxiosRequestConfig,
  AxiosRequestHeaders,
} from "axios";
import { API_BASE_URL } from "../config.ts";
import { useAuth } from "../stores/authStore.ts";

export const api = axios.create({
  baseURL: API_BASE_URL,
});

// Request interceptor to add access token
api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const { accessToken } = useAuth.getState();
  if (accessToken) {
    const headers: AxiosRequestHeaders = (config.headers ??
      {}) as AxiosRequestHeaders;
    headers["Authorization"] = `Bearer ${accessToken}`;
    config.headers = headers;
  }
  return config;
});

// Response interceptor to handle token refresh
api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & {
      _retry?: boolean;
    };

    // If error is 401 and we haven't retried yet
    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      try {
        // Try to refresh the token
        await useAuth.getState().refresh();

        // Retry the original request with new token
        const { accessToken } = useAuth.getState();
        if (accessToken && originalRequest.headers) {
          originalRequest.headers["Authorization"] = `Bearer ${accessToken}`;
        }
        return api(originalRequest);
      } catch (refreshError) {
        // If refresh fails, logout and redirect to login
        useAuth.getState().logout();
        window.location.href = "/login";
        return Promise.reject(refreshError);
      }
    }

    return Promise.reject(error);
  },
);

export default api;
