import {
  httpClient,
  setAccessToken,
  clearAccessToken,
  restoreAccessToken,
} from "./httpClient";
import { UserRole, type User } from "../../features/auth/types";
import type { BaseDto } from "./types";
import { buildPreviewUrl } from "./previewUrl";

interface LoginRequest {
  username: string;
  password: string;
  firstName?: string;
  lastName?: string;
  twoFactorCode?: string;
  trustDevice?: boolean;
}

interface LoginResponse {
  accessToken: string;
}

interface RestoreSessionOptions {
  allowWhenRefreshDisabled?: boolean;
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
  return avatarHashEncryptedHex
    ? buildPreviewUrl(avatarHashEncryptedHex)
    : undefined;
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

  logout: async (): Promise<void> => {
    clearAccessToken();
    await httpClient.post("auth/logout");
  },

  restoreSession: async (
    options: RestoreSessionOptions = {},
  ): Promise<User | null> => {
    const restored = await restoreAccessToken<UserInfoResponse>({
      allowWhenRefreshDisabled: options.allowWhenRefreshDisabled,
    });
    if (!restored) {
      return null;
    }

    return mapUserResponse(restored.user);
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
    await httpClient.post(
      `users/verify-email?token=${encodeURIComponent(token)}`,
    );
  },

  invalidateShareLinks: async (): Promise<void> => {
    await httpClient.post("auth/invalidate-share-links");
  },
};
