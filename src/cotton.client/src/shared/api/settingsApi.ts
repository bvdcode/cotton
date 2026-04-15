import { httpClient } from "./httpClient";
import type { JsonValue } from "../types/json";

export interface PublicServerInfo {
  canCreateInitialAdmin: boolean;
  product: string;
}

export interface ServerSettings {
  maxChunkSizeBytes: number;
  supportedHashAlgorithm: string;
}

interface SetupStatusRaw {
  isServerInitialized?: boolean;
  IsServerInitialized?: boolean;
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

  getIsSetupComplete: async (): Promise<boolean> => {
    const response = await httpClient.get<SetupStatusRaw>(
      "server/settings/is-setup-complete",
    );

    return (
      response.data.isServerInitialized ??
      response.data.IsServerInitialized ??
      true
    );
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
