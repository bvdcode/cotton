import { httpClient } from "./httpClient";
import { isJsonObject, type JsonValue } from "../types/json";

export interface PublicServerInfo {
  canCreateInitialAdmin: boolean;
  product: string;
}

export interface ServerSettings {
  maxChunkSizeBytes: number;
  supportedHashAlgorithm: string;
}

export type StorageType = "Local" | "S3";
export type EmailMode = "None" | "Cloud" | "Custom";
export type ComputionMode = "Local" | "Cloud" | "Remote";
export type StorageSpaceMode = "Optimal" | "Limited" | "Unlimited";
export type GeoIpLookupMode = "Disabled" | "CottonCloud" | "CustomHttp";
export type ServerUsage = "Other" | "Photos" | "Documents" | "Media";

export interface S3Config {
  accessKey: string;
  secretKey: string;
  endpoint: string;
  region: string;
  bucket: string;
}

export interface EmailConfig {
  username: string;
  password: string;
  smtpServer: string;
  port: string;
  fromAddress: string;
  useSSL: boolean;
}

interface SetupStatusRaw {
  isServerInitialized: boolean;
}

interface ChunkSizeRaw {
  maxChunkSizeBytes: number;
}

const storageTypeValues: StorageType[] = ["Local", "S3"];
const emailModeValues: EmailMode[] = ["None", "Cloud", "Custom"];
const computionModeValues: ComputionMode[] = ["Local", "Cloud", "Remote"];
const storageSpaceModeValues: StorageSpaceMode[] = [
  "Optimal",
  "Limited",
  "Unlimited",
];
const geoIpLookupModeValues: GeoIpLookupMode[] = [
  "Disabled",
  "CottonCloud",
  "CustomHttp",
];
const serverUsageValues: ServerUsage[] = [
  "Other",
  "Photos",
  "Documents",
  "Media",
];

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

  if (
    typeof maxChunkSizeBytes !== "number" ||
    !Number.isFinite(maxChunkSizeBytes)
  ) {
    throw new Error("settings response must contain maxChunkSizeBytes");
  }

  return maxChunkSizeBytes;
};

const resolveSupportedHashAlgorithm = (payload: unknown): string => {
  const rawAlgorithms = Array.isArray(payload)
    ? payload
    : (getRecordField(payload, "supportedHashAlgorithms") ??
      getRecordField(payload, "supportedHashAlgorithm"));

  if (typeof rawAlgorithms === "string" && rawAlgorithms.trim().length > 0) {
    return rawAlgorithms.trim();
  }

  if (Array.isArray(rawAlgorithms)) {
    const supportedHashAlgorithm = rawAlgorithms.find(
      (value): value is string =>
        typeof value === "string" && value.trim().length > 0,
    );

    if (supportedHashAlgorithm) {
      return supportedHashAlgorithm.trim();
    }
  }

  throw new Error(
    "settings response must contain at least one supported hash algorithm",
  );
};

const normalizeEnum = <T extends string>(
  value: unknown,
  values: readonly T[],
  fallback: T,
): T => {
  if (typeof value === "number" && Number.isInteger(value)) {
    return values[value] ?? fallback;
  }

  if (typeof value !== "string") {
    return fallback;
  }

  const match = values.find(
    (entry) => entry.toLowerCase() === value.toLowerCase(),
  );
  return match ?? fallback;
};

const getStringField = (payload: unknown, field: string): string => {
  const value = getRecordField(payload, field);
  return typeof value === "string" ? value : "";
};

const getBooleanField = (payload: unknown, field: string): boolean => {
  return getRecordField(payload, field) === true;
};

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
    const response = await httpClient.get<unknown>("server/settings");

    return {
      maxChunkSizeBytes: resolveMaxChunkSizeBytes(response.data),
      supportedHashAlgorithm: resolveSupportedHashAlgorithm(response.data),
    };
  },

  getChunkSize: async (): Promise<number> => {
    const response = await httpClient.get<ChunkSizeRaw | number>(
      "server/settings/chunk-size",
    );
    return resolveMaxChunkSizeBytes(response.data);
  },

  getTelemetry: async (): Promise<boolean> => {
    const response = await httpClient.get<unknown>("server/settings/telemetry");
    return getBooleanField(response.data, "telemetryEnabled");
  },

  setTelemetry: async (enabled: boolean): Promise<void> => {
    await httpClient.patch("server/settings/telemetry", enabled);
  },

  getAllowCrossUserDeduplication: async (): Promise<boolean> => {
    const response = await httpClient.get<unknown>(
      "server/settings/allow-cross-user-deduplication",
    );
    return getBooleanField(response.data, "allowCrossUserDeduplication");
  },

  setAllowCrossUserDeduplication: async (allow: boolean): Promise<void> => {
    await httpClient.patch(
      "server/settings/allow-cross-user-deduplication",
      allow,
    );
  },

  getAllowGlobalIndexing: async (): Promise<boolean> => {
    const response = await httpClient.get<unknown>(
      "server/settings/allow-global-indexing",
    );
    return getBooleanField(response.data, "allowGlobalIndexing");
  },

  setAllowGlobalIndexing: async (allow: boolean): Promise<void> => {
    await httpClient.patch("server/settings/allow-global-indexing", allow);
  },

  getServerUsage: async (): Promise<ServerUsage[]> => {
    const response = await httpClient.get<unknown>(
      "server/settings/server-usage",
    );
    const value = getRecordField(response.data, "serverUsage");
    if (!Array.isArray(value)) {
      return ["Other"];
    }

    return value.map((item) => normalizeEnum(item, serverUsageValues, "Other"));
  },

  setServerUsage: async (usage: ServerUsage[]): Promise<void> => {
    await httpClient.patch("server/settings/server-usage", usage);
  },

  getTimezone: async (): Promise<string> => {
    const response = await httpClient.get<unknown>("server/settings/timezone");
    return getStringField(response.data, "timezone") || "UTC";
  },

  setTimezone: async (timezone: string): Promise<void> => {
    await httpClient.patch("server/settings/timezone", timezone);
  },

  getPublicBaseUrl: async (): Promise<string> => {
    const response = await httpClient.get<unknown>(
      "server/settings/public-base-url",
    );
    return getStringField(response.data, "publicBaseUrl");
  },

  setPublicBaseUrl: async (url: string): Promise<void> => {
    await httpClient.patch("server/settings/public-base-url", url);
  },

  getStorageSpaceMode: async (): Promise<StorageSpaceMode> => {
    const response = await httpClient.get<unknown>(
      "server/settings/storage-space-mode",
    );
    return normalizeEnum(
      getRecordField(response.data, "storageSpaceMode"),
      storageSpaceModeValues,
      "Optimal",
    );
  },

  setStorageSpaceMode: async (mode: StorageSpaceMode): Promise<void> => {
    await httpClient.patch(`server/settings/storage-space-mode/${mode}`);
  },

  getComputionMode: async (): Promise<ComputionMode> => {
    const response = await httpClient.get<unknown>(
      "server/settings/compution-mode",
    );
    return normalizeEnum(
      getRecordField(response.data, "computionMode"),
      computionModeValues,
      "Local",
    );
  },

  setComputionMode: async (mode: ComputionMode): Promise<void> => {
    await httpClient.patch(`server/settings/compution-mode/${mode}`);
  },

  getStorageType: async (): Promise<StorageType> => {
    const response = await httpClient.get<unknown>(
      "server/settings/storage-type",
    );
    return normalizeEnum(
      getRecordField(response.data, "storageType"),
      storageTypeValues,
      "Local",
    );
  },

  setStorageType: async (type: StorageType): Promise<void> => {
    await httpClient.patch(`server/settings/storage-type/${type}`);
  },

  getS3Config: async (): Promise<S3Config> => {
    const response = await httpClient.get<Partial<S3Config>>(
      "server/settings/s3-config",
    );
    return {
      accessKey: response.data.accessKey ?? "",
      secretKey: response.data.secretKey ?? "",
      endpoint: response.data.endpoint ?? "",
      region: response.data.region ?? "",
      bucket: response.data.bucket ?? "",
    };
  },

  setS3Config: async (config: S3Config): Promise<void> => {
    await httpClient.patch("server/settings/s3-config", config);
  },

  getEmailMode: async (): Promise<EmailMode> => {
    const response = await httpClient.get<unknown>("server/settings/email-mode");
    return normalizeEnum(
      getRecordField(response.data, "emailMode"),
      emailModeValues,
      "None",
    );
  },

  setEmailMode: async (mode: EmailMode): Promise<void> => {
    await httpClient.patch(`server/settings/email-mode/${mode}`);
  },

  getEmailConfig: async (): Promise<EmailConfig> => {
    const response = await httpClient.get<Partial<EmailConfig>>(
      "server/settings/email-config",
    );
    return {
      username: response.data.username ?? "",
      password: response.data.password ?? "",
      smtpServer: response.data.smtpServer ?? "",
      port: response.data.port ?? "",
      fromAddress: response.data.fromAddress ?? "",
      useSSL: response.data.useSSL ?? false,
    };
  },

  setEmailConfig: async (config: EmailConfig): Promise<void> => {
    await httpClient.patch("server/settings/email-config", config);
  },

  getGeoIpLookupMode: async (): Promise<GeoIpLookupMode> => {
    const response = await httpClient.get<unknown>(
      "server/settings/geoip-lookup-mode",
    );
    return normalizeEnum(
      getRecordField(response.data, "geoIpLookupMode"),
      geoIpLookupModeValues,
      "Disabled",
    );
  },

  setGeoIpLookupMode: async (mode: GeoIpLookupMode): Promise<void> => {
    await httpClient.patch(`server/settings/geoip-lookup-mode/${mode}`);
  },

  getCustomGeoIpLookupUrl: async (): Promise<string> => {
    const response = await httpClient.get<unknown>(
      "server/settings/custom-geoip-lookup-url",
    );
    return getStringField(response.data, "customGeoIpLookupUrl");
  },

  setCustomGeoIpLookupUrl: async (url: string): Promise<void> => {
    await httpClient.patch("server/settings/custom-geoip-lookup-url", url);
  },

  saveSetupAnswers: async (
    answers: Record<string, JsonValue>,
  ): Promise<void> => {
    const trustedMode = answers.trustedMode === true;
    const telemetry = answers.telemetry === true;
    const storageType = toStorageType(answers.storage);
    const emailMode = toEmailMode(answers.email);
    const computionMode = toComputionMode(answers.computionMode);
    const storageSpaceMode = toStorageSpaceMode(answers.storageSpace);
    const geoIpLookupMode = toGeoIpLookupMode(answers.geoIpLookupMode);

    const usage = Array.isArray(answers.usage)
      ? answers.usage
          .filter((value): value is string => typeof value === "string")
          .map(mapUsageAnswer)
      : (["Other"] satisfies ServerUsage[]);

    await settingsApi.setAllowCrossUserDeduplication(trustedMode);
    await settingsApi.setAllowGlobalIndexing(trustedMode);
    await settingsApi.setServerUsage(usage.length > 0 ? usage : ["Other"]);
    await settingsApi.setTelemetry(telemetry);

    if (storageType === "S3") {
      const s3Config = readFormObject(answers.s3Config);
      await settingsApi.setS3Config({
        endpoint: getFormString(s3Config, "endpoint"),
        region: getFormString(s3Config, "region"),
        bucket: getFormString(s3Config, "bucket"),
        accessKey: getFormString(s3Config, "accessKey"),
        secretKey: getFormString(s3Config, "secretKey"),
      });
    }
    await settingsApi.setStorageType(storageType);

    if (emailMode === "Custom") {
      const emailConfig = readFormObject(answers.emailConfig);
      await settingsApi.setEmailConfig({
        smtpServer: getFormString(emailConfig, "smtpServer"),
        port: getFormString(emailConfig, "port"),
        username: getFormString(emailConfig, "username"),
        password: getFormString(emailConfig, "password"),
        fromAddress: getFormString(emailConfig, "fromAddress"),
        useSSL: getFormBoolean(emailConfig, "useSSL"),
      });
    }
    await settingsApi.setEmailMode(emailMode);

    await settingsApi.setComputionMode(computionMode);

    if (typeof answers.timezone === "string" && answers.timezone) {
      await settingsApi.setTimezone(answers.timezone);
    }

    await settingsApi.setStorageSpaceMode(storageSpaceMode);

    if (geoIpLookupMode === "CustomHttp") {
      const customGeoIpLookupUrl = readFormObject(answers.customGeoIpLookupUrl);
      await settingsApi.setCustomGeoIpLookupUrl(
        getFormString(customGeoIpLookupUrl, "url"),
      );
    }
    await settingsApi.setGeoIpLookupMode(geoIpLookupMode);
  },
};
