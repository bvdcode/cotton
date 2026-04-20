import axios, { type AxiosError, type InternalAxiosRequestConfig } from "axios";
import { getRefreshEnabled } from "../store";
import { isJsonObject, type JsonValue } from "../types/json";
import { toast } from "react-toastify";

export { isAxiosError } from "axios";

type ToastAwareAxiosError = AxiosError & {
  _apiErrorToastDispatched?: boolean;
};

const collectStringMessages = (value: JsonValue, output: string[]): void => {
  if (typeof value === "string") {
    const trimmed = value.trim();
    if (trimmed.length > 0) {
      output.push(trimmed);
    }
    return;
  }

  if (Array.isArray(value)) {
    value.forEach((entry) => collectStringMessages(entry, output));
    return;
  }

  if (!isJsonObject(value)) {
    return;
  }

  Object.values(value).forEach((entry) => collectStringMessages(entry, output));
};

const extractApiErrorMessage = (
  responseData: JsonValue | null | undefined,
): string | null => {
  if (!responseData || !isJsonObject(responseData)) {
    return null;
  }

  const errorsPayload = responseData.errors;
  if (!errorsPayload) {
    return null;
  }

  const messages: string[] = [];
  collectStringMessages(errorsPayload, messages);
  return messages[0] ?? null;
};

const tryDispatchApiErrorToast = (error: AxiosError): void => {
  if (typeof window === "undefined") {
    return;
  }

  const toastAwareError = error as ToastAwareAxiosError;
  if (toastAwareError._apiErrorToastDispatched) {
    return;
  }

  const requestUrl = error.config?.url ?? "";
  if (requestUrl.includes("auth/refresh")) {
    return;
  }

  const responseData = error.response?.data as JsonValue | null | undefined;
  const message = extractApiErrorMessage(responseData);
  if (!message) {
    return;
  }

  const responseStatus = error.response?.status ?? "unknown";
  const toastId = `api-error:${responseStatus}:${requestUrl}:${message}`;
  toast.error(message, { toastId });
  toastAwareError._apiErrorToastDispatched = true;
};

export const hasApiErrorToastBeenDispatched = (error: AxiosError): boolean => {
  const toastAwareError = error as ToastAwareAxiosError;
  return toastAwareError._apiErrorToastDispatched === true;
};

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
    if (!getRefreshEnabled()) {
      clearAccessToken();
      return null;
    }
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
        tryDispatchApiErrorToast(error);
        return Promise.reject(error);
      }

      // Public share links are anonymous and must not trigger auth refresh/logout loops.
      if (url.includes("/layouts/shared/")) {
        tryDispatchApiErrorToast(error);
        return Promise.reject(error);
      }

      // If refresh is disabled (explicit logout), never attempt refresh.
      if (!getRefreshEnabled()) {
        clearAccessToken();
        if (typeof window !== "undefined") {
          window.dispatchEvent(new CustomEvent("auth:logout"));
        }
        tryDispatchApiErrorToast(error);
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

    tryDispatchApiErrorToast(error);
    return Promise.reject(error);
  },
);
