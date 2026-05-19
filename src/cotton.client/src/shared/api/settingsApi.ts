import { getValidated, httpClient } from "./httpClient";
import { isJsonObject, type JsonValue } from "../types/json";
import {
  allowCrossUserDeduplicationSchema,
  allowGlobalIndexingSchema,
  chunkSizeResponseSchema,
  computionModeResponseSchema,
  customGeoIpLookupUrlSchema,
  defaultUserStorageQuotaBytesSchema,
  defaultUserTemplateNodeIdSchema,
  emailConfigSchema,
  emailModeResponseSchema,
  geoIpLookupModeResponseSchema,
  publicBaseUrlSchema,
  publicServerInfoSchema,
  serverSettingsResponseSchema,
  serverUsageListSchema,
  setupStatusSchema,
  s3ConfigSchema,
  storageSpaceModeResponseSchema,
  storageTypeResponseSchema,
  telemetrySettingSchema,
  timezoneSchema,
  type ComputionMode,
  type EmailConfig,
  type EmailMode,
  type GeoIpLookupMode,
  type PublicServerInfo,
  type S3Config,
  type ServerSettings,
  type ServerUsage,
  type StorageSpaceMode,
  type StorageType,
} from "./schemas/serverSettings";

export type {
  ComputionMode,
  EmailConfig,
  EmailMode,
  GeoIpLookupMode,
  PublicServerInfo,
  S3Config,
  ServerSettings,
  ServerUsage,
  StorageSpaceMode,
  StorageType,
} from "./schemas/serverSettings";

const mapUsageAnswer = (value: string): ServerUsage => {
  switch (value.toLowerCase()) {
    case "photos":
      return "Photos";
    case "documents":
      return "Documents";
    case "media":
      return "Media";
    default:
      return "Other";
  }
};

const toStorageType = (value: unknown): StorageType =>
  typeof value === "string" && value.toLowerCase() === "s3" ? "S3" : "Local";

const toEmailMode = (value: unknown): EmailMode => {
  if (typeof value !== "string") return "None";
  if (value.toLowerCase() === "cloud") return "Cloud";
  if (value.toLowerCase() === "custom") return "Custom";
  return "None";
};

const toComputionMode = (value: unknown): ComputionMode => {
  if (typeof value !== "string") return "Local";
  if (value.toLowerCase() === "cloud") return "Cloud";
  if (value.toLowerCase() === "remote") return "Remote";
  return "Local";
};

const toStorageSpaceMode = (value: unknown): StorageSpaceMode => {
  if (typeof value !== "string") return "Optimal";
  if (value.toLowerCase() === "limited") return "Limited";
  if (value.toLowerCase() === "unlimited") return "Unlimited";
  return "Optimal";
};

const toGeoIpLookupMode = (value: unknown): GeoIpLookupMode => {
  if (typeof value !== "string") return "Disabled";
  if (value.toLowerCase() === "cottoncloud") return "CottonCloud";
  if (value.toLowerCase() === "maxmindlocal") return "MaxMindLocal";
  if (value.toLowerCase() === "customhttp") return "CustomHttp";
  return "Disabled";
};

const readFormObject = (
  value: JsonValue | undefined,
): Record<string, JsonValue> =>
  value !== undefined && isJsonObject(value) ? value : {};

const getFormString = (
  form: Record<string, JsonValue>,
  key: string,
): string => {
  const value = form[key];
  return typeof value === "string" ? value : "";
};

const getFormBoolean = (
  form: Record<string, JsonValue>,
  key: string,
): boolean => form[key] === true;

const resolveTrustedModeSettings = (
  value: JsonValue | undefined,
): {
  allowCrossUserDeduplication: boolean;
  allowGlobalIndexing: boolean;
} => {
  if (value === true || value === "family") {
    return {
      allowCrossUserDeduplication: true,
      allowGlobalIndexing: true,
    };
  }

  if (value === "unknown") {
    return {
      allowCrossUserDeduplication: false,
      allowGlobalIndexing: true,
    };
  }

  return {
    allowCrossUserDeduplication: false,
    allowGlobalIndexing: false,
  };
};

const saveBestEffort = async (
  operations: Array<() => Promise<void>>,
): Promise<void> => {
  const results = await Promise.allSettled(
    operations.map((operation) => operation()),
  );

  const failed = results.filter(
    (result): result is PromiseRejectedResult => result.status === "rejected",
  );

  if (failed.length === results.length) {
    throw failed[0].reason;
  }
};

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

  getChunkSize: (): Promise<number> =>
    getValidated(
      "server/settings/chunk-size",
      chunkSizeResponseSchema,
    ),

  getTelemetry: (): Promise<boolean> =>
    getValidated("server/settings/telemetry", telemetrySettingSchema),

  setTelemetry: async (enabled: boolean): Promise<void> => {
    await httpClient.patch("server/settings/telemetry", enabled);
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
    getValidated(
      "server/settings/server-usage",
      serverUsageListSchema,
    ),

  setServerUsage: async (usage: ServerUsage[]): Promise<void> => {
    await httpClient.patch("server/settings/server-usage", usage);
  },

  getTimezone: (): Promise<string> =>
    getValidated("server/settings/timezone", timezoneSchema),

  setTimezone: async (timezone: string): Promise<void> => {
    await httpClient.patch("server/settings/timezone", timezone);
  },

  getPublicBaseUrl: (): Promise<string> =>
    getValidated(
      "server/settings/public-base-url",
      publicBaseUrlSchema,
    ),

  setPublicBaseUrl: async (url: string): Promise<void> => {
    await httpClient.patch("server/settings/public-base-url", url);
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
    await httpClient.patch("server/settings/default-user-template-node", nodeId);
  },

  getComputionMode: (): Promise<ComputionMode> =>
    getValidated(
      "server/settings/compution-mode",
      computionModeResponseSchema,
    ),

  setComputionMode: async (mode: ComputionMode): Promise<void> => {
    await httpClient.patch(`server/settings/compution-mode/${mode}`);
  },

  getStorageType: (): Promise<StorageType> =>
    getValidated(
      "server/settings/storage-type",
      storageTypeResponseSchema,
    ),

  setStorageType: async (type: StorageType): Promise<void> => {
    await httpClient.patch(`server/settings/storage-type/${type}`);
  },

  getS3Config: (): Promise<S3Config> =>
    getValidated(
      "server/settings/s3-config",
      s3ConfigSchema,
    ),

  setS3Config: async (config: S3Config): Promise<void> => {
    await httpClient.patch("server/settings/s3-config", config);
  },

  getEmailMode: (): Promise<EmailMode> =>
    getValidated("server/settings/email-mode", emailModeResponseSchema),

  setEmailMode: async (mode: EmailMode): Promise<void> => {
    await httpClient.patch(`server/settings/email-mode/${mode}`);
  },

  getEmailConfig: (): Promise<EmailConfig> =>
    getValidated(
      "server/settings/email-config",
      emailConfigSchema,
    ),

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

  testCustomGeoIpLookupUrl: async (): Promise<void> => {
    await httpClient.post("server/settings/custom-geoip-lookup-url/test");
  },

  saveSetupStep: async (
    stepKey: string,
    answers: Record<string, JsonValue>,
  ): Promise<void> => {
    switch (stepKey) {
      case "trustedMode": {
        const {
          allowCrossUserDeduplication,
          allowGlobalIndexing,
        } = resolveTrustedModeSettings(answers.trustedMode);
        await saveBestEffort([
          () =>
            settingsApi.setAllowCrossUserDeduplication(
              allowCrossUserDeduplication,
            ),
          () => settingsApi.setAllowGlobalIndexing(allowGlobalIndexing),
        ]);
        return;
      }

      case "usage": {
        const usage = Array.isArray(answers.usage)
          ? answers.usage
              .filter((value): value is string => typeof value === "string")
              .map(mapUsageAnswer)
          : (["Other"] satisfies ServerUsage[]);

        await settingsApi.setServerUsage(usage.length > 0 ? usage : ["Other"]);
        return;
      }

      case "telemetry":
        await settingsApi.setTelemetry(answers.telemetry === true);
        return;

      case "geoIpLookupMode": {
        const geoIpLookupMode = toGeoIpLookupMode(answers.geoIpLookupMode);
        if (geoIpLookupMode !== "CustomHttp") {
          await settingsApi.setGeoIpLookupMode(geoIpLookupMode);
        }
        return;
      }

      case "customGeoIpLookupUrl": {
        const customGeoIpLookupUrl = readFormObject(
          answers.customGeoIpLookupUrl,
        );
        await settingsApi.setCustomGeoIpLookupUrl(
          getFormString(customGeoIpLookupUrl, "url"),
        );
        await settingsApi.setGeoIpLookupMode("CustomHttp");
        await settingsApi.testCustomGeoIpLookupUrl();
        return;
      }

      case "storage": {
        const storageType = toStorageType(answers.storage);
        if (storageType !== "S3") {
          await settingsApi.setStorageType(storageType);
        }
        return;
      }

      case "s3Config": {
        const s3Config = readFormObject(answers.s3Config);
        await settingsApi.setS3Config({
          endpoint: getFormString(s3Config, "endpoint"),
          region: getFormString(s3Config, "region"),
          bucket: getFormString(s3Config, "bucket"),
          accessKey: getFormString(s3Config, "accessKey"),
          secretKey: getFormString(s3Config, "secretKey"),
        });
        await settingsApi.setStorageType("S3");
        return;
      }

      case "email": {
        const emailMode = toEmailMode(answers.email);
        if (emailMode !== "Custom") {
          await settingsApi.setEmailMode(emailMode);
        }
        return;
      }

      case "emailConfig": {
        const emailConfig = readFormObject(answers.emailConfig);
        await settingsApi.setEmailConfig({
          smtpServer: getFormString(emailConfig, "smtpServer"),
          port: getFormString(emailConfig, "port"),
          username: getFormString(emailConfig, "username"),
          password: getFormString(emailConfig, "password"),
          fromAddress: getFormString(emailConfig, "fromAddress"),
          useSSL: getFormBoolean(emailConfig, "useSSL"),
        });
        await settingsApi.setEmailMode("Custom");
        await settingsApi.testEmailConfig();
        return;
      }

      case "computionMode":
        await settingsApi.setComputionMode(
          toComputionMode(answers.computionMode),
        );
        return;

      case "timezone":
        if (typeof answers.timezone === "string" && answers.timezone) {
          await settingsApi.setTimezone(answers.timezone);
        }
        return;

      case "storageSpace":
        await settingsApi.setStorageSpaceMode(
          toStorageSpaceMode(answers.storageSpace),
        );
        return;

      default:
        return;
    }
  },

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
