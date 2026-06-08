import { httpClient } from "./httpClient";

export interface AppCodeDetails {
  id: string;
  applicationName: string;
  applicationVersion: string;
  deviceName?: string | null;
  origin: string;
  requestedAt: string;
  expiresAt: string;
  status: string;
}

export const appCodeApi = {
  getDetails: async (id: string): Promise<AppCodeDetails> => {
    const response = await httpClient.get<AppCodeDetails>(
      `oauth/app-code/${encodeURIComponent(id)}`,
    );
    return response.data;
  },

  approve: async (id: string): Promise<void> => {
    await httpClient.post(`oauth/app-code/${encodeURIComponent(id)}/approve`);
  },

  deny: async (id: string): Promise<void> => {
    await httpClient.post(`oauth/app-code/${encodeURIComponent(id)}/deny`);
  },
};
