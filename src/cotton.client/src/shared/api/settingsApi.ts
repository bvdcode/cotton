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

const getRecordField = (value: unknown, field: string): unknown => {
  if (!value || typeof value !== "object") {
    return undefined;
  }

  return (value as Record<string, unknown>)[field];
};

const resolveMaxChunkSizeBytes = (payload: unknown): number => {
  const maxChunkSizeBytes =
    typeof payload === "number"
      ? payload
      : getRecordField(payload, "maxChunkSizeBytes");

  if (typeof maxChunkSizeBytes !== "number" || !Number.isFinite(maxChunkSizeBytes)) {
    throw new Error("chunk-size response must contain maxChunkSizeBytes");
  }

  return maxChunkSizeBytes;
};

const resolveSupportedHashAlgorithm = (payload: unknown): string => {
  const rawAlgorithms = Array.isArray(payload)
    ? payload
    : (getRecordField(payload, "supportedHashAlgorithms") ??
      getRecordField(payload, "supportedHashAlgorithm"));

  if (typeof rawAlgorithms === "string" && rawAlgorithms.trim().length > 0) {
    return rawAlgorithms;
  }

  if (Array.isArray(rawAlgorithms)) {
    const supportedHashAlgorithm = rawAlgorithms.find(
      (value): value is string =>
        typeof value === "string" && value.trim().length > 0,
    );

    if (supportedHashAlgorithm) {
      return supportedHashAlgorithm;
    }
  }

  throw new Error(
    "supported-hash-algorithms response must contain at least one string value",
  );
};

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
        httpClient.get<ChunkSizeRaw | number>("server/settings/chunk-size"),
        httpClient.get<unknown>(
          "server/settings/supported-hash-algorithms",
        ),
      ]);

    return {
      maxChunkSizeBytes: resolveMaxChunkSizeBytes(chunkSizeResponse.data),
      supportedHashAlgorithm: resolveSupportedHashAlgorithm(
        supportedHashAlgorithmsResponse.data,
      ),
    };
  },

  saveSetupAnswers: async (
    answers: Record<string, JsonValue>,
  ): Promise<void> => {
    await httpClient.post("server/settings", answers);
  },
};
