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
  isServerInitialized: boolean;
}

interface ChunkSizeRaw {
  maxChunkSizeBytes: number;
}

interface SupportedHashAlgorithmsRaw {
  supportedHashAlgorithms: string[];
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

    return response.data.isServerInitialized;
  },

  get: async (): Promise<ServerSettings> => {
    const [chunkSizeResponse, supportedHashAlgorithmsResponse] =
      await Promise.all([
        httpClient.get<ChunkSizeRaw>("server/settings/chunk-size"),
        httpClient.get<SupportedHashAlgorithmsRaw>(
          "server/settings/supported-hash-algorithms",
        ),
      ]);

    const [supportedHashAlgorithm] =
      supportedHashAlgorithmsResponse.data.supportedHashAlgorithms;

    if (!supportedHashAlgorithm) {
      throw new Error(
        "supportedHashAlgorithms must contain at least one value",
      );
    }

    return {
      maxChunkSizeBytes: chunkSizeResponse.data.maxChunkSizeBytes,
      supportedHashAlgorithm,
    };
  },

  saveSetupAnswers: async (
    answers: Record<string, JsonValue>,
  ): Promise<void> => {
    await httpClient.post("server/settings", answers);
  },
};
