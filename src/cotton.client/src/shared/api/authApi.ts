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

interface UpdateProfileRequest {
  avatarHash?: string | null;
  username?: string | null;
  email?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  birthDate?: string | null;
}

/**
 * User info response matching backend UserDto : BaseDto<Guid>
 */
interface UserInfoResponse extends BaseDto<string> {
  username: string;
  email?: string | null;
  isEmailVerified?: boolean;
  role: UserRole;
  displayName?: string;
  pictureUrl?: string;
  avatarHashEncryptedHex?: string | null;

  preferences?: Record<string, string>;

  firstName?: string | null;
  lastName?: string | null;
  birthDate?: string | null;

  // 2FA (TOTP)
  isTotpEnabled?: boolean;
  totpEnabledAt?: string | null;
  totpFailedAttempts?: number;
}

const buildAvatarUrl = (response: UserInfoResponse): string | undefined => {
  const avatarHashEncryptedHex = response.avatarHashEncryptedHex?.trim();
  if (avatarHashEncryptedHex) {
    return `/api/v1/preview/${encodeURIComponent(avatarHashEncryptedHex)}.webp`;
  }

  return response.pictureUrl;
};

const mapUserResponse = (response: UserInfoResponse): User => {
  return {
    id: response.id,
    role: response.role,
    username: response.username,
    email: response.email ?? null,
    isEmailVerified: response.isEmailVerified ?? false,
    displayName: response.displayName ?? response.username,
    pictureUrl: buildAvatarUrl(response),
    avatarHashEncryptedHex: response.avatarHashEncryptedHex ?? null,
    preferences: response.preferences,
    firstName: response.firstName ?? null,
    lastName: response.lastName ?? null,
    birthDate: response.birthDate ?? null,
    createdAt: response.createdAt,
    updatedAt: response.updatedAt,
    isTotpEnabled: response.isTotpEnabled,
    totpEnabledAt: response.totpEnabledAt ?? null,
    totpFailedAttempts: response.totpFailedAttempts ?? 0,
  };
};

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

    return mapUserResponse(response.data);
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

  updateProfile: async (request: UpdateProfileRequest): Promise<User> => {
    const response = await httpClient.put<UserInfoResponse>(
      "users/me",
      request,
    );
    return mapUserResponse(response.data);
  },

  forgotPassword: async (usernameOrEmail: string): Promise<void> => {
    await httpClient.post("auth/forgot-password", { usernameOrEmail });
  },

  resetPassword: async (token: string, newPassword: string): Promise<void> => {
    await httpClient.post("auth/reset-password", { token, newPassword });
  },

  sendEmailVerification: async (): Promise<void> => {
    await httpClient.post("users/me/send-email-verification");
  },

  confirmEmailVerification: async (token: string): Promise<void> => {
    await httpClient.post(`users/verify-email?token=${encodeURIComponent(token)}`);
  },

  invalidateShareLinks: async (): Promise<void> => {
    await httpClient.post("auth/invalidate-share-links");
  },
};
