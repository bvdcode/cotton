import axios from "axios";
import type { InternalAxiosRequestConfig, AxiosRequestHeaders } from "axios";
import { API_BASE_URL } from "../config.ts";
import { useAuth } from "../stores/authStore.ts";

export const api = axios.create({
  baseURL: API_BASE_URL,
});

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const { token } = useAuth.getState();
  if (token) {
    const headers: AxiosRequestHeaders = (config.headers ?? {}) as AxiosRequestHeaders;
    headers["Authorization"] = `Bearer ${token}`;
    config.headers = headers;
  }
  return config;
});

export default api;
