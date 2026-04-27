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

interface ChunkSizeRaw {
  maxChunkSizeBytes: number;
}

interface SupportedHashAlgorithmsRaw {
  supportedHashAlgorithms?: string | string[];
  SupportedHashAlgorithms?: string | string[];
}

const DEFAULT_SUPPORTED_HASH_ALGORITHM = "SHA-256";

const pickSupportedHashAlgorithm = (
  raw: SupportedHashAlgorithmsRaw,
): string => {
  const candidates = raw.supportedHashAlgorithms ?? raw.SupportedHashAlgorithms;

  if (typeof candidates === "string" && candidates.trim().length > 0) {
    return candidates;
  }

  if (Array.isArray(candidates)) {
    const first = candidates.find(
      (algorithm) => typeof algorithm === "string" && algorithm.trim().length > 0,
    );
    if (first) {
      return first;
    }
  }

  return DEFAULT_SUPPORTED_HASH_ALGORITHM;
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
    const [chunkSizeResponse, supportedHashAlgorithmsResponse] =
      await Promise.all([
        httpClient.get<ChunkSizeRaw>("server/settings/chunk-size"),
        httpClient.get<SupportedHashAlgorithmsRaw>(
          "server/settings/supported-hash-algorithms",
        ),
      ]);

    const supportedHashAlgorithm = pickSupportedHashAlgorithm(
      supportedHashAlgorithmsResponse.data,
    );

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
