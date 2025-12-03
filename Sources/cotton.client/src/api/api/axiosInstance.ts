import axios, {
  AxiosError,
  AxiosInstance,
  InternalAxiosRequestConfig,
} from "axios";
import type { ReactKitProps, TokenPair } from "../types";
import { useAuthStore } from "../store/authStore";

let isRefreshing = false;
let failedQueue: Array<{
  resolve: (token: string) => void;
  reject: (error: any) => void;
}> = [];

const processQueue = (error: any, token: string | null = null) => {
  failedQueue.forEach((promise) => {
    if (error) {
      promise.reject(error);
    } else {
      promise.resolve(token!);
    }
  });
  failedQueue = [];
};

export class AuthenticatedAxiosInstance {
  private axiosInstance: AxiosInstance;
  private plainAxiosInstance: AxiosInstance;
  private props: ReactKitProps;
  private accessToken: string | null = null;

  constructor(props: ReactKitProps) {
    this.props = props;
    this.axiosInstance = axios.create({
      baseURL: props.baseURL,
    });
    this.plainAxiosInstance = axios.create({
      baseURL: props.baseURL,
    });

    this.setupInterceptors();
    this.restoreAccessTokenFromStore();
  }

  private restoreAccessTokenFromStore(): void {
    const accessToken = useAuthStore.getState().accessToken;
    if (accessToken) {
      this.accessToken = accessToken;
    }
  }

  public getRefreshToken(): string | null {
    return useAuthStore.getState().getRefreshToken();
  }

  private setTokensInStore(tokens: TokenPair): void {
    useAuthStore.getState().setRefreshToken(tokens.refreshToken);
    useAuthStore.getState().setAccessToken(tokens.accessToken);
  }

  private clearTokensInStore(): void {
    useAuthStore.getState().clearTokens();
  }

  private async performRefresh(): Promise<string> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      throw new Error("No refresh token available");
    }
    if (!this.props.authConfig?.refreshToken) {
      throw new Error("refreshToken handler is not configured");
    }

    const tokens = await this.props.authConfig.refreshToken(
      refreshToken,
      this.plainAxiosInstance,
    );
    this.accessToken = tokens.accessToken;
    this.setTokensInStore(tokens);
    return tokens.accessToken;
  }

  private setupInterceptors() {
    // Request interceptor - queue if refresh in progress, perform refresh if no access token
    this.axiosInstance.interceptors.request.use(
      async (config: InternalAxiosRequestConfig) => {
        // If refresh already running, wait in queue
        if (isRefreshing) {
          const token = await new Promise<string>((resolve, reject) => {
            failedQueue.push({ resolve, reject });
          });
          if (config.headers) {
            config.headers.Authorization = `Bearer ${token}`;
          }
          return config;
        }

        // If no access token but have refresh token - start refresh
        if (!this.accessToken && this.getRefreshToken()) {
          isRefreshing = true;
          try {
            const token = await this.performRefresh();
            processQueue(null, token);
            if (config.headers) {
              config.headers.Authorization = `Bearer ${token}`;
            }
          } catch (error) {
            processQueue(error, null);
            this.handleUnauthorized();
            return Promise.reject(error);
          } finally {
            isRefreshing = false;
          }
          return config;
        }

        // Normal case: have access token
        if (this.accessToken && config.headers) {
          config.headers.Authorization = `Bearer ${this.accessToken}`;
        }
        return config;
      },
      (error) => Promise.reject(error),
    );

    // Response interceptor - handle 401 error
    this.axiosInstance.interceptors.response.use(
      (response) => response,
      async (error: AxiosError) => {
        const originalRequest = error.config as InternalAxiosRequestConfig & {
          _retry?: boolean;
        };

        // If 401 error and not a retry request
        if (error.response?.status === 401 && !originalRequest._retry) {
          originalRequest._retry = true;

          // If refresh already running, wait in queue
          if (isRefreshing) {
            return new Promise((resolve, reject) => {
              failedQueue.push({ resolve, reject });
            })
              .then((token) => {
                if (originalRequest.headers) {
                  originalRequest.headers.Authorization = `Bearer ${token}`;
                }
                return this.axiosInstance(originalRequest);
              })
              .catch((err) => Promise.reject(err));
          }

          // Start refresh
          isRefreshing = true;
          try {
            const token = await this.performRefresh();
            processQueue(null, token);

            // Retry the original request
            if (originalRequest.headers) {
              originalRequest.headers.Authorization = `Bearer ${token}`;
            }
            return this.axiosInstance(originalRequest);
          } catch (refreshError) {
            processQueue(refreshError, null);
            this.handleUnauthorized();
            return Promise.reject(refreshError);
          } finally {
            isRefreshing = false;
          }
        }

        return Promise.reject(error);
      },
    );
  }

  private handleUnauthorized() {
    const refreshToken = this.getRefreshToken();

    if (this.props.authConfig?.logout) {
      const result = this.props.authConfig.logout(
        refreshToken,
        this.plainAxiosInstance,
      );
      if (result instanceof Promise) {
        result.catch((err: unknown) => {
          console.error("Logout handler failed:", err);
        });
      }
    }

    this.accessToken = null;
    this.clearTokensInStore();
  }

  public setAccessToken(accessToken: string): void {
    this.accessToken = accessToken;
  }

  public setTokens(tokens: TokenPair): void {
    this.accessToken = tokens.accessToken;
    this.setTokensInStore(tokens);
  }

  public clearTokens(): void {
    this.accessToken = null;
    this.clearTokensInStore();
  }

  public getAxiosInstance(): AxiosInstance {
    return this.axiosInstance;
  }

  public hasTokens(): boolean {
    return !!(this.accessToken && this.getRefreshToken());
  }

  public getAccessToken(): string | null {
    return this.accessToken;
  }
}
