import { httpClient } from "./httpClient";

export interface ServerSettings {
  serverHasUsers: boolean;
  maxChunkSizeBytes: number;
  isServerInitialized: boolean;
  supportedHashAlgorithm: string;
}

interface ServerSettingsRaw {
  serverHasUsers: boolean;
  maxChunkSizeBytes: number;
  isServerInitialized: boolean;
  SupportedHashAlgorithm?: string;
  supportedHashAlgorithm?: string;
}

export const settingsApi = {
  get: async (): Promise<ServerSettings> => {
    const response = await httpClient.get<ServerSettingsRaw>("settings");

    const supportedHashAlgorithm =
      response.data.supportedHashAlgorithm ??
      response.data.SupportedHashAlgorithm ??
      "Unknown";

    return {
      serverHasUsers: response.data.serverHasUsers,
      maxChunkSizeBytes: response.data.maxChunkSizeBytes,
      isServerInitialized: response.data.isServerInitialized,
      supportedHashAlgorithm,
    };
  },

  saveSetupAnswers: async (answers: Record<string, unknown>): Promise<void> => {
    await httpClient.post("settings", answers);
  },
};
