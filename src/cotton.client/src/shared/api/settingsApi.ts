import {
  mergeCloudflareMetadata,
  mergeDetectedProxyServices,
} from "./proxyDetection";
import { saveSetupStep } from "./setupSettingsApi";
import { getValidated, httpClient, parseValidated } from "./httpClient";
import { computationStatusSchema, type ComputationStatus } from "./computation";
import type { JsonValue } from "../types/json";
import {
  allowCrossUserDeduplicationSchema,
  allowGlobalIndexingSchema,
  chunkSizeResponseSchema,
  chunkSizeSettingsResponseSchema,
  computionModeResponseSchema,
  customGeoIpLookupTestResultSchema,
  customGeoIpLookupUrlSchema,
  defaultUserStorageQuotaBytesSchema,
  defaultUserTemplateNodeIdSchema,
  disableVersionCheckSettingSchema,
  emailConfigSchema,
  emailModeResponseSchema,
  geoIpLookupModeResponseSchema,
  publicBaseUrlSchema,
  observedProxyInfoSchema,
  publicServerInfoSchema,
  remoteComputationRunnerUrlSchema,
  serverSettingsResponseSchema,
  serverUsageListSchema,
  setupStatusSchema,
  s3ConfigSchema,
  storagePipelineSettingsResponseSchema,
  storageSpaceModeResponseSchema,
  storageTypeResponseSchema,
  telemetrySettingSchema,
  timezoneSchema,
  trustedProxyIpAddressSchema,
  trustedProxyVerificationResultSchema,
  type ChunkSizeSettings,
  type ComputionMode,
  type CustomGeoIpLookupTestResult,
  type EmailConfig,
  type EmailMode,
  type GeoIpLookupMode,
  type ObservedProxyInfo,
  type PublicServerInfo,
  type S3Config,
  type ServerSettings,
  type ServerUsage,
  type StoragePipelineSettings,
  type StorageSpaceMode,
  type StorageType,
  type TrustedProxyVerificationResult,
} from "./schemas/serverSettings";

export { DIRECT_CONNECTION_IP_ADDRESS } from "./schemas/serverSettings";

export type {
  ChunkSizeSettings,
  CloudflareProxyMetadata,
  ComputionMode,
  CustomGeoIpLookupTestResult,
  EmailConfig,
  EmailMode,
  GeoIpLookupMode,
  DetectedProxyService,
  ObservedProxyInfo,
  PublicServerInfo,
  S3Config,
  ServerSettings,
  ServerUsage,
  StoragePipelineSettings,
  StorageSpaceMode,
  StorageType,
  TrustedProxyVerificationResult,
} from "./schemas/serverSettings";

const setupStepOrder = [
  "trustedMode",
  "usage",
  "telemetry",
  "geoIpLookupMode",
  "customGeoIpLookupUrl",
  "storage",
  "s3Config",
  "email",
  "emailConfig",
  "computionMode",
  "remoteComputationRunnerUrl",
  "timezone",
  "storageSpace",
] as const;

export const settingsApi = {
  getPublicInfo: (): Promise<PublicServerInfo> =>
    getValidated("server/info", publicServerInfoSchema),

  getIsSetupComplete: async (): Promise<boolean> => {
    const response = await getValidated(
      "server/settings/is-setup-complete",
      setupStatusSchema,
    );

    return response.isServerInitialized;
  },

  get: (): Promise<ServerSettings> =>
    getValidated("server/settings", serverSettingsResponseSchema),

  getChunkSizeSettings: (): Promise<ChunkSizeSettings> =>
    getValidated("server/settings/chunk-size", chunkSizeSettingsResponseSchema),

  getChunkSize: (): Promise<number> =>
    getValidated("server/settings/chunk-size", chunkSizeResponseSchema),

  setChunkSize: async (
    maxChunkSizeBytes: number,
  ): Promise<ChunkSizeSettings> => {
    const response = await httpClient.patch<unknown>(
      `server/settings/chunk-size/${maxChunkSizeBytes}`,
    );
    return parseValidated(
      "server/settings/chunk-size",
      response.data,
      chunkSizeSettingsResponseSchema,
    );
  },

  getStoragePipelineSettings: (): Promise<StoragePipelineSettings> =>
    getValidated(
      "server/settings/storage-pipeline",
      storagePipelineSettingsResponseSchema,
    ),

  setCompressionLevel: async (
    compressionLevel: number,
  ): Promise<StoragePipelineSettings> => {
    const response = await httpClient.patch<unknown>(
      `server/settings/compression-level/${compressionLevel}`,
    );
    return parseValidated(
      "server/settings/compression-level",
      response.data,
      storagePipelineSettingsResponseSchema,
    );
  },

  setCipherChunkSize: async (
    cipherChunkSizeBytes: number,
  ): Promise<StoragePipelineSettings> => {
    const response = await httpClient.patch<unknown>(
      `server/settings/cipher-chunk-size/${cipherChunkSizeBytes}`,
    );
    return parseValidated(
      "server/settings/cipher-chunk-size",
      response.data,
      storagePipelineSettingsResponseSchema,
    );
  },

  setEncryptionThreads: async (
    encryptionThreads: number,
  ): Promise<StoragePipelineSettings> => {
    const response = await httpClient.patch<unknown>(
      `server/settings/encryption-threads/${encryptionThreads}`,
    );
    return parseValidated(
      "server/settings/encryption-threads",
      response.data,
      storagePipelineSettingsResponseSchema,
    );
  },

  getTelemetry: (): Promise<boolean> =>
    getValidated("server/settings/telemetry", telemetrySettingSchema),

  setTelemetry: async (enabled: boolean): Promise<void> => {
    await httpClient.patch("server/settings/telemetry", enabled);
  },

  getDisableVersionCheck: (): Promise<boolean> =>
    getValidated(
      "server/settings/disable-version-check",
      disableVersionCheckSettingSchema,
    ),

  setDisableVersionCheck: async (disabled: boolean): Promise<void> => {
    await httpClient.patch("server/settings/disable-version-check", disabled);
  },

  getAllowCrossUserDeduplication: (): Promise<boolean> =>
    getValidated(
      "server/settings/allow-cross-user-deduplication",
      allowCrossUserDeduplicationSchema,
    ),

  setAllowCrossUserDeduplication: async (allow: boolean): Promise<void> => {
    await httpClient.patch(
      "server/settings/allow-cross-user-deduplication",
      allow,
    );
  },

  getAllowGlobalIndexing: (): Promise<boolean> =>
    getValidated(
      "server/settings/allow-global-indexing",
      allowGlobalIndexingSchema,
    ),

  setAllowGlobalIndexing: async (allow: boolean): Promise<void> => {
    await httpClient.patch("server/settings/allow-global-indexing", allow);
  },

  getServerUsage: (): Promise<ServerUsage[]> =>
    getValidated("server/settings/server-usage", serverUsageListSchema),

  setServerUsage: async (usage: ServerUsage[]): Promise<void> => {
    await httpClient.patch("server/settings/server-usage", usage);
  },

  getTimezone: (): Promise<string> =>
    getValidated("server/settings/timezone", timezoneSchema),

  setTimezone: async (timezone: string): Promise<void> => {
    await httpClient.patch("server/settings/timezone", timezone);
  },

  getPublicBaseUrl: (): Promise<string> =>
    getValidated("server/settings/public-base-url", publicBaseUrlSchema),

  setPublicBaseUrl: async (url: string): Promise<void> => {
    await httpClient.patch("server/settings/public-base-url", url);
  },

  getTrustedProxyIpAddress: (): Promise<string> =>
    getValidated(
      "server/settings/trusted-proxy-ip-address",
      trustedProxyIpAddressSchema,
    ),

  getObservedProxyInfo: async (): Promise<ObservedProxyInfo> => {
    const path = "server/settings/trusted-proxy-ip-address/observed";
    const response = await httpClient.get<unknown>(path);
    const result = parseValidated(path, response.data, observedProxyInfoSchema);
    return {
      ...result,
      detectedProxyServices: mergeDetectedProxyServices(
        result.detectedProxyServices,
        response.headers.server,
      ),
      cloudflare: mergeCloudflareMetadata(
        result.cloudflare,
        response.headers["cf-ray"],
      ),
    };
  },

  verifyAndSaveTrustedProxyIpAddress: async (
    ipAddress: string | null,
  ): Promise<TrustedProxyVerificationResult> => {
    const response = await httpClient.post<unknown>(
      "server/settings/trusted-proxy-ip-address/verify-and-save",
      ipAddress,
    );
    const result = parseValidated(
      "server/settings/trusted-proxy-ip-address/verify-and-save",
      response.data,
      trustedProxyVerificationResultSchema,
    );
    return {
      ...result,
      detectedProxyServices: mergeDetectedProxyServices(
        result.detectedProxyServices,
        response.headers.server,
      ),
      cloudflare: mergeCloudflareMetadata(
        result.cloudflare,
        response.headers["cf-ray"],
      ),
    };
  },

  getStorageSpaceMode: (): Promise<StorageSpaceMode> =>
    getValidated(
      "server/settings/storage-space-mode",
      storageSpaceModeResponseSchema,
    ),

  setStorageSpaceMode: async (mode: StorageSpaceMode): Promise<void> => {
    await httpClient.patch(`server/settings/storage-space-mode/${mode}`);
  },

  getDefaultUserStorageQuotaBytes: (): Promise<number | null> =>
    getValidated(
      "server/settings/default-user-storage-quota-bytes",
      defaultUserStorageQuotaBytesSchema,
    ),

  setDefaultUserStorageQuotaBytes: async (
    quotaBytes: number | null,
  ): Promise<void> => {
    await httpClient.patch(
      "server/settings/default-user-storage-quota-bytes",
      quotaBytes,
    );
  },

  getDefaultUserTemplateNodeId: (): Promise<string | null> =>
    getValidated(
      "server/settings/default-user-template-node",
      defaultUserTemplateNodeIdSchema,
    ),

  setDefaultUserTemplateNodeId: async (
    nodeId: string | null,
  ): Promise<void> => {
    await httpClient.patch(
      "server/settings/default-user-template-node",
      nodeId,
    );
  },

  getComputionMode: (): Promise<ComputionMode> =>
    getValidated("server/settings/compution-mode", computionModeResponseSchema),

  setComputionMode: async (
    mode: ComputionMode,
  ): Promise<ComputationStatus | null> => {
    const path = `server/settings/compution-mode/${mode}`;
    const response = await httpClient.patch<unknown>(path);
    if (response.status === 204) {
      return null;
    }
    return parseValidated(path, response.data, computationStatusSchema);
  },

  getRemoteComputationRunnerUrl: (): Promise<string> =>
    getValidated(
      "server/settings/remote-computation-runner-url",
      remoteComputationRunnerUrlSchema,
    ),

  getComputationStatus: (signal?: AbortSignal): Promise<ComputationStatus> =>
    getValidated(
      "server/settings/computation-status",
      computationStatusSchema,
      { signal },
    ),

  setRemoteComputationRunnerUrl: async (
    url: string,
  ): Promise<ComputationStatus> => {
    const response = await httpClient.patch(
      "server/settings/remote-computation-runner-url",
      url.trim(),
    );
    return parseValidated(
      "server/settings/remote-computation-runner-url",
      response.data,
      computationStatusSchema,
    );
  },

  getStorageType: (): Promise<StorageType> =>
    getValidated("server/settings/storage-type", storageTypeResponseSchema),

  setStorageType: async (type: StorageType): Promise<void> => {
    await httpClient.patch(`server/settings/storage-type/${type}`);
  },

  getS3Config: (): Promise<S3Config> =>
    getValidated("server/settings/s3-config", s3ConfigSchema),

  setS3Config: async (config: S3Config): Promise<void> => {
    await httpClient.patch("server/settings/s3-config", config);
  },

  getEmailMode: (): Promise<EmailMode> =>
    getValidated("server/settings/email-mode", emailModeResponseSchema),

  setEmailMode: async (mode: EmailMode): Promise<void> => {
    await httpClient.patch(`server/settings/email-mode/${mode}`);
  },

  getEmailConfig: (): Promise<EmailConfig> =>
    getValidated("server/settings/email-config", emailConfigSchema),

  setEmailConfig: async (config: EmailConfig): Promise<void> => {
    await httpClient.patch("server/settings/email-config", config);
  },

  testEmailConfig: async (): Promise<void> => {
    await httpClient.post("server/settings/email-config/test");
  },

  getGeoIpLookupMode: (): Promise<GeoIpLookupMode> =>
    getValidated(
      "server/settings/geoip-lookup-mode",
      geoIpLookupModeResponseSchema,
    ),

  setGeoIpLookupMode: async (mode: GeoIpLookupMode): Promise<void> => {
    await httpClient.patch(`server/settings/geoip-lookup-mode/${mode}`);
  },

  getCustomGeoIpLookupUrl: (): Promise<string> =>
    getValidated(
      "server/settings/custom-geoip-lookup-url",
      customGeoIpLookupUrlSchema,
    ),

  setCustomGeoIpLookupUrl: async (url: string): Promise<void> => {
    await httpClient.patch("server/settings/custom-geoip-lookup-url", url);
  },

  testCustomGeoIpLookupUrl: async (): Promise<CustomGeoIpLookupTestResult> => {
    const url = "server/settings/custom-geoip-lookup-url/test";
    const response = await httpClient.post<unknown>(url);
    return parseValidated(
      url,
      response.data,
      customGeoIpLookupTestResultSchema,
    );
  },

  saveSetupStep: (
    stepKey: string,
    answers: Record<string, JsonValue>,
  ): Promise<void> => saveSetupStep(stepKey, answers, settingsApi),

  saveSetupAnswers: async (
    answers: Record<string, JsonValue>,
  ): Promise<void> => {
    for (const stepKey of setupStepOrder) {
      try {
        await settingsApi.saveSetupStep(stepKey, answers);
      } catch (error) {
        console.warn(`Failed to save setup step "${stepKey}"`, error);
      }
    }
  },
};
