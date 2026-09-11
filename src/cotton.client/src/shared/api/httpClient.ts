import axios, {
  type AxiosError,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from "axios";
import { z } from "zod";
import { getRefreshEnabled, useAuthStore } from "../store/authStore";
import { toast } from "@shared/ui/notifications";
import { translateError } from "../i18n/translateError";
import {
  authSessionResponseSchema,
  type AuthSessionResponse,
} from "./authSession";

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

const extractApiValidationErrorMessage = (
  responseData: unknown,
): string | null => {
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

export const extractApiErrorMessage = (
  responseData: unknown,
): string | null => {
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

const dispatchApiErrorToast = (error: AxiosError, message: string): void => {
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
  if (
    typeof Intl === "undefined" ||
    typeof Intl.DateTimeFormat !== "function"
  ) {
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
let accessTokenRevision = 0;
let logoutEventDispatched = false;
let unlockRedirectDispatched = false;
let refreshPromise: Promise<AuthSessionResponse | null> | null = null;

const resetAuthTransportState = (): void => {
  logoutEventDispatched = false;
};

const dispatchLogoutEventOnce = (): void => {
  if (logoutEventDispatched || typeof window === "undefined") {
    return;
  }

  logoutEventDispatched = true;
  window.dispatchEvent(new CustomEvent("auth:logout"));
};

const isMissingRefreshSession = (error: unknown): boolean => {
  if (!axios.isAxiosError(error)) {
    return false;
  }

  return error.response?.status === 404;
};

const disableRefreshAndLogout = (): void => {
  clearAccessToken();
  useAuthStore.getState().logoutLocal();
  dispatchLogoutEventOnce();
};

const isServerLockedResponse = (error: AxiosError): boolean =>
  error.response?.status === 423 &&
  isRecord(error.response.data) &&
  error.response.data.locked === true;

const redirectToUnlockOnce = (): void => {
  if (unlockRedirectDispatched || typeof window === "undefined") {
    return;
  }

  if (window.location.pathname === "/unlock") {
    return;
  }

  unlockRedirectDispatched = true;
  window.location.assign("/unlock");
};

export const getAccessToken = () => accessToken;
export const setAccessToken = (token: string | null) => {
  accessToken = token;
  accessTokenRevision += 1;
  refreshPromise = null;
  if (token) {
    resetAuthTransportState();
  }
};
export const clearAccessToken = () => {
  accessToken = null;
  accessTokenRevision += 1;
  refreshPromise = null;
};

export interface RefreshAccessTokenOptions {
  allowWhenRefreshDisabled?: boolean;
}

const performTokenRefresh = async (): Promise<AuthSessionResponse | null> => {
  const revision = accessTokenRevision;
  try {
    const response = await httpClient.post<object>(
      "auth/refresh",
      {},
      { withCredentials: true },
    );
    const session = parseValidated(
      "auth/refresh",
      response.data,
      authSessionResponseSchema,
    );
    if (revision !== accessTokenRevision) {
      return null;
    }

    setAccessToken(session.accessToken);
    return session;
  } catch (error) {
    if (isMissingRefreshSession(error)) {
      if (revision === accessTokenRevision) {
        disableRefreshAndLogout();
      }
      return null;
    }

    if (error instanceof z.ZodError && revision === accessTokenRevision) {
      clearAccessToken();
    }

    throw error;
  }
};

const requestTokenRefresh = (
  options: RefreshAccessTokenOptions = {},
): Promise<AuthSessionResponse | null> => {
  const refreshAllowed =
    options.allowWhenRefreshDisabled || getRefreshEnabled();
  if (!refreshAllowed) {
    clearAccessToken();
    return Promise.resolve(null);
  }

  if (refreshPromise) {
    return refreshPromise;
  }

  const promise = performTokenRefresh();
  refreshPromise = promise;
  const clearPromise = (): void => {
    if (refreshPromise === promise) {
      refreshPromise = null;
    }
  };
  void promise.then(clearPromise, clearPromise);
  return promise;
};

export const refreshAccessToken = async (
  options: RefreshAccessTokenOptions = {},
): Promise<string | null> => {
  const payload = await requestTokenRefresh(options);
  return payload?.accessToken ?? null;
};

export const restoreAuthSession = async (
  options: RefreshAccessTokenOptions = {},
): Promise<AuthSessionResponse | null> => {
  return await requestTokenRefresh(options);
};

export const httpClient = axios.create({
  baseURL: "/api/v1",
  timeout: 60000,
  withCredentials: true,
  headers: {
    "Content-Type": "application/json",
  },
});

const SCHEMA_VALIDATION_TOAST_ID = "api-schema-validation";

const reportSchemaFailure = (url: string, error: z.ZodError): void => {
  console.error(`[httpClient] Schema validation failed for ${url}:`, error);

  if (typeof window !== "undefined") {
    toast.error(translateError("common", "errors.schemaValidationFailed"), {
      toastId: `${SCHEMA_VALIDATION_TOAST_ID}:${url}`,
    });
  }
};

export const parseValidated = <TSchema extends z.ZodTypeAny>(
  url: string,
  data: unknown,
  schema: TSchema,
): z.infer<TSchema> => {
  const result = schema.safeParse(data);

  if (!result.success) {
    reportSchemaFailure(url, result.error);
    throw result.error;
  }

  return result.data;
};

export const getValidated = async <TSchema extends z.ZodTypeAny>(
  url: string,
  schema: TSchema,
  config?: AxiosRequestConfig,
): Promise<z.infer<TSchema>> => {
  const response = await httpClient.get<unknown>(url, config);
  return parseValidated(url, response.data, schema);
};

const retriedRequests = new WeakSet<InternalAxiosRequestConfig>();

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
    const originalRequest = error.config;
    if (!originalRequest) {
      tryDispatchApiErrorToast(error);
      return Promise.reject(error);
    }
    const url = originalRequest.url || "";

    if (isServerLockedResponse(error)) {
      redirectToUnlockOnce();
      return Promise.reject(error);
    }

    if (
      error.response?.status === 401 &&
      !retriedRequests.has(originalRequest)
    ) {
      // Don't retry on auth endpoints themselves
      if (url.includes("auth/refresh")) {
        return Promise.reject(error);
      }
      if (url.includes("auth/login")) {
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
        disableRefreshAndLogout();
        tryDispatchApiErrorToast(error);
        return Promise.reject(error);
      }

      retriedRequests.add(originalRequest);

      try {
        const newToken = await refreshAccessToken();

        if (newToken) {
          if (originalRequest.headers) {
            originalRequest.headers.Authorization = `Bearer ${newToken}`;
          }
          return httpClient(originalRequest);
        }
      } catch {
        return Promise.reject(error);
      }

      return Promise.reject(error);
    }

    if (url.includes("auth/refresh")) {
      return Promise.reject(error);
    }

    tryDispatchApiErrorToast(error);
    return Promise.reject(error);
  },
);
