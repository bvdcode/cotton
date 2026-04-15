import { httpClient } from "./httpClient";
import type { JsonValue } from "../types/json";

export interface PublicServerInfo {
  uptime: string;
  serverHasUsers: boolean;
  product: string;
}

export interface ServerSettings {
  maxChunkSizeBytes: number;
  supportedHashAlgorithm: string;
}

interface ServerSettingsRaw {
  maxChunkSizeBytes: number;
  SupportedHashAlgorithm?: string;
  supportedHashAlgorithm?: string;
}

export const settingsApi = {
  getPublicInfo: async (): Promise<PublicServerInfo> => {
    const response = await httpClient.get<PublicServerInfo>("server/info");
    return response.data;
  },

  get: async (): Promise<ServerSettings> => {
    const response = await httpClient.get<ServerSettingsRaw>("server/settings");

    const supportedHashAlgorithm =
      response.data.supportedHashAlgorithm ??
      response.data.SupportedHashAlgorithm ??
      "Unknown";

    return {
      maxChunkSizeBytes: response.data.maxChunkSizeBytes,
      supportedHashAlgorithm,
    };
  },

  saveSetupAnswers: async (
    answers: Record<string, JsonValue>,
  ): Promise<void> => {
    await httpClient.post("server/settings", answers);
  },
};
