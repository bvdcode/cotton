import {
  httpClient,
  setAccessToken,
  clearAccessToken,
  refreshAccessToken,
} from "./httpClient";
import { UserRole, type User } from "../../features/auth/types";
import type { BaseDto } from "./types";

interface LoginRequest {
  username: string;
  password: string;
  twoFactorCode?: string;
  trustDevice?: boolean;
}

interface LoginResponse {
  accessToken: string;
}

interface ChangePasswordRequest {
  oldPassword: string;
  newPassword: string;
}

/**
 * User info response matching backend UserDto : BaseDto<Guid>
 */
interface UserInfoResponse extends BaseDto<string> {
  username: string;
  email?: string | null;
  role: UserRole;
  displayName?: string;
  pictureUrl?: string;

  preferences?: Record<string, string>;

  // 2FA (TOTP)
  isTotpEnabled?: boolean;
  totpEnabledAt?: string | null;
  totpFailedAttempts?: number;
}

export const authApi = {
  /**
   * Login with username/password
   */
  login: async (credentials: LoginRequest): Promise<string> => {
    const response = await httpClient.post<LoginResponse>(
      "auth/login",
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

    // Validate critical fields from BaseDto
    if (!response.data.createdAt || !response.data.updatedAt) {
      console.error(
        "Missing required BaseDto fields from /auth/me:",
        response.data,
      );
    }

    return {
      id: response.data.id,
      role: response.data.role,
      username: response.data.username,
      email: response.data.email ?? null,
      displayName: response.data.displayName ?? response.data.username,
      pictureUrl: response.data.pictureUrl,
      preferences: response.data.preferences,
      createdAt: response.data.createdAt,
      updatedAt: response.data.updatedAt,

      isTotpEnabled: response.data.isTotpEnabled,
      totpEnabledAt: response.data.totpEnabledAt ?? null,
      totpFailedAttempts: response.data.totpFailedAttempts ?? 0,
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

  getWebDavToken: async (): Promise<string> => {
    const response = await httpClient.get<string>("auth/webdav/token");
    return response.data;
  },

  changePassword: async (request: ChangePasswordRequest): Promise<void> => {
    await httpClient.put("users/me/password", request);
  },
};
