import { httpClient, isAxiosError } from "./httpClient";
import type { BaseDto } from "./types";
import { UserRole } from "../../features/auth/types";

export interface AdminUserDto extends BaseDto<string> {
  username: string;
  email: string | null;
  role: UserRole;
  firstName: string | null;
  lastName: string | null;
  birthDate: string | null;
  isTotpEnabled: boolean;
  totpEnabledAt: string | null;
  totpFailedAttempts: number;
  lastActivityAt: string | null;
  activeSessionCount: number;
}

export interface AdminCreateUserRequestDto {
  username: string;
  email: string | null;
  password: string;
  role: UserRole;
  firstName: string | null;
  lastName: string | null;
  birthDate: string | null;
}

export interface AdminUpdateUserRequestDto {
  username: string;
  email: string | null;
  role: UserRole;
  firstName: string | null;
  lastName: string | null;
  birthDate: string | null;
}

export interface LatestDatabaseBackupDto {
  backupId: string;
  createdAtUtc: string;
  pointerUpdatedAtUtc: string;
  dumpSizeBytes: number;
  chunkCount: number;
  dumpContentHash: string;
  sourceDatabase: string;
  sourceHost: string;
  sourcePort: number;
}

export const adminApi = {
  getUsers: async (): Promise<AdminUserDto[]> => {
    const response = await httpClient.get<AdminUserDto[]>("users");
    return response.data;
  },

  createUser: async (request: AdminCreateUserRequestDto): Promise<void> => {
    await httpClient.post("users", request);
  },

  updateUser: async (
    userId: string,
    request: AdminUpdateUserRequestDto,
  ): Promise<AdminUserDto> => {
    const response = await httpClient.put<AdminUserDto>(
      `users/${userId}`,
      request,
    );
    return response.data;
  },

  getLatestDatabaseBackup:
    async (): Promise<LatestDatabaseBackupDto | null> => {
      try {
        const response = await httpClient.get<LatestDatabaseBackupDto>(
          "server/database-backup/latest",
        );
        return response.data;
      } catch (error) {
        if (isAxiosError(error) && error.response?.status === 404) {
          return null;
        }
        throw error;
      }
    },

  triggerDatabaseBackup: async (): Promise<void> => {
    await httpClient.patch("server/database-backup/trigger");
  },
};
