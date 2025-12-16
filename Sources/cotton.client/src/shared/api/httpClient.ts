import axios, { type AxiosError, type InternalAxiosRequestConfig } from "axios";

let accessToken: string | null = null;
export const getAccessToken = () => accessToken;
export const setAccessToken = (token: string | null) => {
  accessToken = token;
};
export const clearAccessToken = () => {
  accessToken = null;
};

/**
 * Refreshes access token using refresh cookie.
 * Returns new token or null if refresh failed.
 */
export const refreshAccessToken = async (): Promise<string | null> => {
  try {
    const response = await httpClient.post(
      "auth/refresh",
      {},
      { withCredentials: true },
    );
    const token = response.data?.accessToken;
    if (token && typeof token === "string" && token.length > 0) {
      setAccessToken(token);
      return token;
    }
    clearAccessToken();
    return null;
  } catch {
    clearAccessToken();
    return null;
  }
};

// Create axios instance
export const httpClient = axios.create({
  baseURL: "/api/v1",
  timeout: 60000,
  withCredentials: true,
  headers: {
    "Content-Type": "application/json",
  },
});

// Refresh state
let isRefreshing = false;
let refreshQueue: Array<(token: string | null) => void> = [];

const processQueue = (token: string | null) => {
  refreshQueue.forEach((resolve) => resolve(token));
  refreshQueue = [];
};

// Request interceptor - attach token
httpClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    if (accessToken && config.headers) {
      config.headers.Authorization = `Bearer ${accessToken}`;
    }
    return config;
  },
  (error) => Promise.reject(error),
);

// Response interceptor - handle 401 with refresh queue
httpClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & {
      _retry?: boolean;
    };

    // Check if 401 and not already retrying
    if (error.response?.status === 401 && !originalRequest._retry) {
      // Don't retry on auth endpoints themselves
      const url = originalRequest.url || "";
      if (url.includes("auth/login") || url.includes("auth/refresh")) {
        return Promise.reject(error);
      }

      originalRequest._retry = true;

      if (isRefreshing) {
        // Queue request until refresh completes
        return new Promise((resolve, reject) => {
          refreshQueue.push((token: string | null) => {
            if (token) {
              if (originalRequest.headers) {
                originalRequest.headers.Authorization = `Bearer ${token}`;
              }
              resolve(httpClient(originalRequest));
            } else {
              reject(error);
            }
          });
        });
      }

      isRefreshing = true;

      try {
        const newToken = await refreshAccessToken();

        if (newToken) {
          processQueue(newToken);

          // Retry original request with new token
          if (originalRequest.headers) {
            originalRequest.headers.Authorization = `Bearer ${newToken}`;
          }
          return httpClient(originalRequest);
        } else {
          throw new Error("Refresh token failed");
        }
      } catch (refreshError) {
        // Refresh failed - clear token and queue
        clearAccessToken();
        processQueue(null);

        // Trigger logout from auth context if available
        if (typeof window !== "undefined") {
          window.dispatchEvent(new CustomEvent("auth:logout"));
        }

        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error);
  },
);
