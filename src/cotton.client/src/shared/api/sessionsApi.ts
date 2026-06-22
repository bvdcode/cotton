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
  getSessions: async (): Promise<SessionDto[]> => {
    const response = await httpClient.get<SessionDto[]>("auth/sessions");
    return response.data;
  },

  revokeSession: async (sessionId: string): Promise<void> => {
    await httpClient.delete(`auth/sessions/${sessionId}`);
  },
};
