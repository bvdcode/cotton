import {
  httpClient,
  setAccessToken,
  clearAccessToken,
  restoreAuthSession,
  parseValidated,
} from "./httpClient";
import type { RestoreResult, User } from "../../features/auth/types";
import {
  authSessionResponseSchema,
  mapUserResponse,
  type UserInfoResponse,
} from "./authSession";

interface LoginRequest {
  username: string;
  password: string;
  firstName?: string;
  lastName?: string;
  twoFactorCode?: string;
  trustDevice?: boolean;
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

export const authApi = {
  login: async (credentials: LoginRequest): Promise<User> => {
    const url = "auth/login";
    const response = await httpClient.post<object>(url, credentials);
    const session = parseValidated(
      url,
      response.data,
      authSessionResponseSchema,
    );
    setAccessToken(session.accessToken);
    return mapUserResponse(session.user);
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
  ): Promise<RestoreResult> => {
    const restored = await restoreAuthSession({
      allowWhenRefreshDisabled: options.allowWhenRefreshDisabled,
    });
    if (!restored) {
      return { kind: "anonymous" };
    }

    return {
      kind: "authenticated",
      user: mapUserResponse(restored.user),
    };
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
