import { httpClient } from "./httpClient";
import type { BaseDto } from "./types";
import { UserRole } from "../../features/auth/types";

export interface AdminUserDto extends BaseDto<string> {
  username: string;
  email: string | null;
  role: UserRole;
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
}

export const adminApi = {
  getUsers: async (): Promise<AdminUserDto[]> => {
    const response = await httpClient.get<AdminUserDto[]>("users");
    return response.data;
  },

  createUser: async (request: AdminCreateUserRequestDto): Promise<void> => {
    await httpClient.post("users", request);
  },
};
