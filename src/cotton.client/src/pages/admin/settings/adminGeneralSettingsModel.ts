import type {
  ComputionMode,
  GeoIpLookupMode,
  ServerUsage,
  StorageSpaceMode,
} from "../../../shared/api/settingsApi";

export type GeneralSettingKey =
  | "publicBaseUrl"
  | "timezone"
  | "telemetry"
  | "allowDeduplication"
  | "allowGlobalIndexing"
  | "serverUsage"
  | "storageSpaceMode"
  | "geoIpLookupMode"
  | "customGeoIpLookupUrl";

export type GeneralSettingsValidationMessages = {
  required: string;
  publicBaseUrlInvalid: string;
  timezoneInvalid: string;
  customGeoIpLookupUrlInvalid: string;
  customGeoIpLookupUrlRequiresIp: string;
};

type PublicBaseUrlValidation = {
  error: string | null;
  normalized: string | null;
};

type TextUrlValidation = {
  error: string | null;
  normalized: string | null;
};

export const usageOptions: ServerUsage[] = [
  "Photos",
  "Documents",
  "Media",
  "Other",
];

export const computionOptions: ComputionMode[] = ["Local", "Remote", "Cloud"];

export const storageSpaceOptions: StorageSpaceMode[] = [
  "Limited",
  "Optimal",
  "Unlimited",
];

export const geoIpOptions: GeoIpLookupMode[] = [
  "Disabled",
  "CottonCloud",
  "CustomHttp",
];

const fallbackTimeZones = [
  "UTC",
  "America/Los_Angeles",
  "America/New_York",
  "Europe/London",
  "Europe/Berlin",
  "Europe/Madrid",
  "Europe/Moscow",
  "Asia/Dubai",
  "Asia/Tokyo",
  "Australia/Sydney",
];

export const getSupportedTimeZones = (): string[] => {
  const intlWithSupportedValues = Intl as typeof Intl & {
    supportedValuesOf?: (key: "timeZone") => string[];
  };

  if (typeof intlWithSupportedValues.supportedValuesOf !== "function") {
    return fallbackTimeZones;
  }

  const supported = intlWithSupportedValues.supportedValuesOf("timeZone");
  const withUtc = supported.includes("UTC") ? supported : ["UTC", ...supported];
  return Array.from(new Set(withUtc)).sort((a, b) => a.localeCompare(b));
};

const normalizeUrlForSave = (url: URL): string => {
  url.hash = "";
  url.search = "";

  const normalized = url.toString();
  return normalized.endsWith("/") ? normalized.slice(0, -1) : normalized;
};

const isHttpUrl = (url: URL): boolean =>
  url.protocol === "http:" || url.protocol === "https:";

export const normalizeStoredPublicBaseUrl = (value: string): string => {
  const trimmed = value.trim();
  if (!trimmed) return "";

  try {
    const url = new URL(trimmed);
    if (
      !isHttpUrl(url) ||
      url.username ||
      url.password ||
      url.search ||
      url.hash
    ) {
      return trimmed;
    }

    return normalizeUrlForSave(url);
  } catch {
    return trimmed;
  }
};

export const validatePublicBaseUrl = (
  value: string,
  messages: GeneralSettingsValidationMessages,
): PublicBaseUrlValidation => {
  const trimmed = value.trim();
  if (!trimmed) {
    return {
      error: messages.required,
      normalized: null,
    };
  }

  try {
    const url = new URL(trimmed);
    if (
      !isHttpUrl(url) ||
      url.username ||
      url.password ||
      url.search ||
      url.hash
    ) {
      return {
        error: messages.publicBaseUrlInvalid,
        normalized: null,
      };
    }

    const normalized = normalizeUrlForSave(url);
    return {
      error: null,
      normalized,
    };
  } catch {
    return {
      error: messages.publicBaseUrlInvalid,
      normalized: null,
    };
  }
};

export const validateTimezone = (
  value: string,
  validTimeZones: ReadonlySet<string>,
  messages: GeneralSettingsValidationMessages,
): string | null => {
  const trimmed = value.trim();
  if (!trimmed) return messages.required;
  return validTimeZones.has(trimmed) ? null : messages.timezoneInvalid;
};

export const validateCustomGeoIpLookupUrl = (
  value: string,
  required: boolean,
  messages: GeneralSettingsValidationMessages,
): TextUrlValidation => {
  const trimmed = value.trim();
  if (!required) {
    return { error: null, normalized: trimmed };
  }

  if (!trimmed) {
    return { error: messages.required, normalized: null };
  }

  if (!trimmed.includes("{ip}")) {
    return {
      error: messages.customGeoIpLookupUrlRequiresIp,
      normalized: null,
    };
  }

  try {
    const probe = trimmed.split("{ip}").join("127.0.0.1");
    const url = new URL(probe);
    if (!isHttpUrl(url)) {
      return {
        error: messages.customGeoIpLookupUrlInvalid,
        normalized: null,
      };
    }
  } catch {
    return {
      error: messages.customGeoIpLookupUrlInvalid,
      normalized: null,
    };
  }

  return { error: null, normalized: trimmed };
};

export const isSameArray = <T,>(
  left: readonly T[],
  right: readonly T[],
): boolean =>
  left.length === right.length &&
  left.every((item, index) => item === right[index]);
