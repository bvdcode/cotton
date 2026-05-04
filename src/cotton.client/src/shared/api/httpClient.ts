import axios, { type AxiosError, type InternalAxiosRequestConfig } from "axios";
import { getRefreshEnabled, useAuthStore } from "../store/authStore";
import { toast } from "react-toastify";

export { isAxiosError } from "axios";

type ToastAwareAxiosError = AxiosError & {
  _apiErrorToastDispatched?: boolean;
};

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const normalizeMessage = (value: unknown): string | null => {
  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
};

const collectStringMessages = (value: unknown, output: string[]): void => {
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

  if (!isRecord(value)) {
    return;
  }

  Object.values(value).forEach((entry) => collectStringMessages(entry, output));
};

const extractApiValidationErrorMessage = (responseData: unknown): string | null => {
  if (!isRecord(responseData)) {
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

export const extractApiErrorMessage = (responseData: unknown): string | null => {
  const plainTextMessage = normalizeMessage(responseData);
  if (plainTextMessage) {
    return plainTextMessage;
  }

  if (!isRecord(responseData)) {
    return null;
  }

  return (
    normalizeMessage(responseData.detail) ??
    normalizeMessage(responseData.message) ??
    extractApiValidationErrorMessage(responseData) ??
    normalizeMessage(responseData.title)
  );
};

export const getApiErrorMessage = (error: unknown): string | null => {
  if (!axios.isAxiosError(error)) {
    return null;
  }

  return extractApiErrorMessage(error.response?.data);
};

const dispatchApiErrorToast = (
  error: AxiosError,
  message: string,
): void => {
  if (typeof window === "undefined") {
    return;
  }

  const toastAwareError = error as ToastAwareAxiosError;
  if (toastAwareError._apiErrorToastDispatched) {
    return;
  }

  const requestUrl = error.config?.url ?? "";
  const responseStatus = error.response?.status ?? "unknown";
  const toastId = `api-error:${responseStatus}:${requestUrl}:${message}`;
  toast.error(message, { toastId });
  toastAwareError._apiErrorToastDispatched = true;
};

const tryDispatchApiErrorToast = (error: AxiosError): void => {
  const requestUrl = error.config?.url ?? "";
  if (requestUrl.includes("auth/refresh")) {
    return;
  }

  const message = extractApiValidationErrorMessage(error.response?.data);
  if (!message) {
    return;
  }

  dispatchApiErrorToast(error, message);
};

export const hasApiErrorToastBeenDispatched = (error: AxiosError): boolean => {
  const toastAwareError = error as ToastAwareAxiosError;
  return toastAwareError._apiErrorToastDispatched === true;
};

export const showApiErrorToast = (
  error: unknown,
  fallbackMessage: string,
  toastId: string,
): void => {
  if (axios.isAxiosError(error)) {
    if (hasApiErrorToastBeenDispatched(error)) {
      return;
    }

    const message = getApiErrorMessage(error);
    if (message) {
      dispatchApiErrorToast(error, message);
      return;
    }
  }

  toast.error(fallbackMessage, { toastId });
};

const resolveBrowserTimeZone = (): string | null => {
  if (typeof Intl === "undefined" || typeof Intl.DateTimeFormat !== "function") {
    return null;
  }

  const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  if (typeof timeZone !== "string") {
    return null;
  }

  const normalizedTimeZone = timeZone.trim();
  return normalizedTimeZone.length > 0 ? normalizedTimeZone : null;
};

const browserTimeZone = resolveBrowserTimeZone();

let accessToken: string | null = null;
let refreshBlocked = false;
let logoutEventDispatched = false;

const resetAuthTransportState = (): void => {
  refreshBlocked = false;
  logoutEventDispatched = false;
};

const dispatchLogoutEventOnce = (): void => {
  if (logoutEventDispatched || typeof window === "undefined") {
    return;
  }

  logoutEventDispatched = true;
  window.dispatchEvent(new CustomEvent("auth:logout"));
};

const isTerminalRefreshFailure = (error: unknown): boolean => {
  if (!axios.isAxiosError(error)) {
    return false;
  }

  const status = error.response?.status;
  return status === 400 || status === 401 || status === 403 || status === 404;
};

const disableRefreshAndLogout = (): void => {
  clearAccessToken();
  useAuthStore.getState().logoutLocal();
  dispatchLogoutEventOnce();
};

export const getAccessToken = () => accessToken;
export const setAccessToken = (token: string | null) => {
  accessToken = token;
  if (token) {
    resetAuthTransportState();
  }
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
    if (!getRefreshEnabled() || refreshBlocked) {
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
  } catch (error) {
    if (isTerminalRefreshFailure(error)) {
      refreshBlocked = true;
      disableRefreshAndLogout();
      return null;
    }

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

    if (browserTimeZone && config.headers) {
      config.headers["X-Timezone"] = browserTimeZone;
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
      if (!getRefreshEnabled() || refreshBlocked) {
        disableRefreshAndLogout();
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
        processQueue(null);

        disableRefreshAndLogout();

        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    tryDispatchApiErrorToast(error);
    return Promise.reject(error);
  },
);
