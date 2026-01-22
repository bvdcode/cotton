import { httpClient } from "./httpClient";

export interface SessionDto {
  sessionId: string;
  ipAddress?: string;
  userAgent?: string;
  authType?: string;
  country: string;
  region: string;
  city: string;
  device: string;
  refreshTokenCount: number;
  totalSessionDuration: string; // TimeSpan from C#
  lastSeenAt: string; // ISO date string
  isCurrentSession: boolean;
}

export const sessionsApi = {
  /**
   * Get all active sessions for the current user
   */
  getSessions: async (): Promise<SessionDto[]> => {
    const response = await httpClient.get<SessionDto[]>("/auth/sessions");
    return response.data;
  },

  /**
   * Revoke a specific session
   */
  revokeSession: async (sessionId: string): Promise<void> => {
    await httpClient.delete(`/auth/sessions/${sessionId}`);
  },
};
