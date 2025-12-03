import type {
  UserInfo,
  TokenPair,
  ReactKitProps,
  LoginCredentials,
} from "../types";
import type { AxiosInstance } from "axios";
import { AuthenticatedAxiosInstance } from "./axiosInstance";

export class ApiService {
  private authAxios: AuthenticatedAxiosInstance;
  private config: ReactKitProps;

  constructor(config: ReactKitProps) {
    this.config = config;
    this.authAxios = new AuthenticatedAxiosInstance(config);
  }

  /**
   * Get axios instance for external code usage
   */
  public getAxios(): AxiosInstance {
    return this.authAxios.getAxiosInstance();
  }

  /**
   * Login - external code must provide a handler
   */
  public async login(credentials: LoginCredentials): Promise<TokenPair> {
    if (!this.config.authConfig?.login) {
      throw new Error("onLogin handler is not configured");
    }

    const tokens = await this.config.authConfig.login(
      credentials,
      this.getAxios(),
    );
    this.authAxios.setTokens(tokens);
    return tokens;
  }

  /**
   * Get user info - external code must provide a handler
   */
  public async getUserInfo(): Promise<UserInfo> {
    if (!this.config.authConfig?.getUserInfo) {
      throw new Error("onGetUserInfo handler is not configured");
    }

    return await this.config.authConfig.getUserInfo(this.getAxios());
  }

  /**
   * Logout - clears tokens and calls the handler
   */
  public async logout(): Promise<void> {
    const refreshToken = this.authAxios.getRefreshToken();

    if (this.config.authConfig?.logout) {
      try {
        await this.config.authConfig.logout(refreshToken, this.getAxios());
      } catch {}
    }

    this.authAxios.clearTokens();
  }

  /**
   * Check if there are active tokens
   */
  public isAuthenticated(): boolean {
    return this.authAxios.hasTokens();
  }

  /**
   * Get access token
   */
  public getAccessToken(): string | null {
    return this.authAxios.getAccessToken();
  }
}
