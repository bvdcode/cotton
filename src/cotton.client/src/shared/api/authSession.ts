import { z } from "zod";
import { UserRole, type User } from "../../features/auth/types";
import { buildPreviewUrl } from "./previewUrl";

const userInfoResponseSchema = z.object({
  id: z.string(),
  createdAt: z.string(),
  updatedAt: z.string(),
  username: z.string(),
  email: z.string().nullable().optional(),
  isEmailVerified: z.boolean().optional(),
  role: z.union([z.literal(UserRole.User), z.literal(UserRole.Admin)]),
  displayName: z.string().optional(),
  avatarHashEncryptedHex: z.string().nullable().optional(),
  preferences: z.record(z.string(), z.string()).optional(),
  firstName: z.string().nullable().optional(),
  lastName: z.string().nullable().optional(),
  birthDate: z.string().nullable().optional(),
  isTotpEnabled: z.boolean().optional(),
  totpEnabledAt: z.string().nullable().optional(),
  totpFailedAttempts: z.number().optional(),
});

export const authSessionResponseSchema = z.object({
  accessToken: z.string().min(1),
  refreshToken: z.string().min(1),
  user: userInfoResponseSchema,
});

export type AuthSessionResponse = z.infer<typeof authSessionResponseSchema>;
export type UserInfoResponse = z.infer<typeof userInfoResponseSchema>;

const buildAvatarUrl = (response: UserInfoResponse): string | undefined => {
  const avatarHashEncryptedHex = response.avatarHashEncryptedHex?.trim();
  return avatarHashEncryptedHex
    ? buildPreviewUrl(avatarHashEncryptedHex)
    : undefined;
};

export const mapUserResponse = (response: UserInfoResponse): User => ({
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
});
