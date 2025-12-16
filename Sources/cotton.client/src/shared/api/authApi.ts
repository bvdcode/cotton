import {
  httpClient,
  setAccessToken,
  clearAccessToken,
  refreshAccessToken,
} from "./httpClient";
import type { User } from "../../features/auth/types";

interface LoginRequest {
  username: string;
  password: string;
}

interface LoginResponse {
  accessToken: string;
}

interface UserInfoResponse {
  id: string;
  username: string;
  displayName?: string;
  pictureUrl?: string;
}

export const authApi = {
  /**
   * Login with username/password
   */
  login: async (credentials: LoginRequest): Promise<string> => {
    const response = await httpClient.post<LoginResponse>(
      "/auth/login",
      credentials,
    );
    const token = response.data.accessToken;
    setAccessToken(token);
    return token;
  },

  /**
   * Get current user info - validates token
   */
  me: async (): Promise<User> => {
    const response = await httpClient.get<UserInfoResponse>("auth/me");
    return {
      id: response.data.id ?? "",
      username: response.data.username ?? "Unknown",
      displayName: response.data.displayName ?? response.data.username,
      pictureUrl: response.data.pictureUrl,
    };
  },

  /**
   * Logout - clear token
   */
  logout: async (): Promise<void> => {
    clearAccessToken();
    await httpClient.post("auth/logout");
  },

  /**
   * Tries to refresh access token using backend refresh cookie.
   * Safe to call on app startup; errors are swallowed.
   * Returns token if successful, null otherwise.
   */
  refresh: async (): Promise<string | null> => {
    return await refreshAccessToken();
  },
};
