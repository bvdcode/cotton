import { isJsonObject, type JsonValue } from "../types/json";
import type { ComputationStatus } from "./computation";
import type {
  ServerUsage,
  EmailMode,
  ComputionMode,
  StorageSpaceMode,
  GeoIpLookupMode,
  StorageType,
  S3Config,
  EmailConfig,
} from "./schemas/serverSettings";

interface SetupSettingsApi {
  setAllowCrossUserDeduplication(value: boolean): Promise<void>;
  setAllowGlobalIndexing(value: boolean): Promise<void>;
  setServerUsage(value: ServerUsage[]): Promise<void>;
  setTelemetry(value: boolean): Promise<void>;
  setGeoIpLookupMode(value: GeoIpLookupMode): Promise<void>;
  setCustomGeoIpLookupUrl(value: string): Promise<void>;
  testCustomGeoIpLookupUrl(): Promise<JsonValue>;
  setStorageType(value: StorageType): Promise<void>;
  setS3Config(value: S3Config): Promise<void>;
  setEmailMode(value: EmailMode): Promise<void>;
  setEmailConfig(value: EmailConfig): Promise<void>;
  testEmailConfig(): Promise<void>;
  setComputionMode(value: ComputionMode): Promise<ComputationStatus | null>;
  setRemoteComputationRunnerUrl(value: string): Promise<ComputationStatus>;
  setTimezone(value: string): Promise<void>;
  setStorageSpaceMode(value: StorageSpaceMode): Promise<void>;
}

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

const toStorageType = (value: JsonValue): StorageType =>
  typeof value === "string" && value.toLowerCase() === "s3" ? "S3" : "Local";

const toEmailMode = (value: JsonValue): EmailMode => {
  if (typeof value !== "string") return "None";
  if (value.toLowerCase() === "cloud") return "Cloud";
  if (value.toLowerCase() === "custom") return "Custom";
  return "None";
};

const toComputionMode = (value: JsonValue): ComputionMode => {
  if (typeof value !== "string") return "Local";
  if (value.toLowerCase() === "cloud") return "Cloud";
  if (value.toLowerCase() === "remote") return "Remote";
  return "Local";
};

const toStorageSpaceMode = (value: JsonValue): StorageSpaceMode => {
  if (typeof value !== "string") return "Optimal";
  if (value.toLowerCase() === "limited") return "Limited";
  if (value.toLowerCase() === "unlimited") return "Unlimited";
  return "Optimal";
};

const toGeoIpLookupMode = (value: JsonValue): GeoIpLookupMode => {
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

export const saveSetupStep = async (
  stepKey: string,
  answers: Record<string, JsonValue>,
  settingsApi: SetupSettingsApi,
): Promise<void> => {
  switch (stepKey) {
    case "trustedMode": {
      const { allowCrossUserDeduplication, allowGlobalIndexing } =
        resolveTrustedModeSettings(answers.trustedMode);
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
      const customGeoIpLookupUrl = readFormObject(answers.customGeoIpLookupUrl);
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

    case "computionMode": {
      const computionMode = toComputionMode(answers.computionMode);
      if (computionMode !== "Remote") {
        await settingsApi.setComputionMode(computionMode);
      }
      return;
    }

    case "remoteComputationRunnerUrl": {
      const remoteRunner = readFormObject(answers.remoteComputationRunnerUrl);
      await settingsApi.setRemoteComputationRunnerUrl(
        getFormString(remoteRunner, "url").trim(),
      );
      return;
    }

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
};
