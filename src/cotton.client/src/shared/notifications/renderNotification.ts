import type { TFunction } from "i18next";
import i18n from "../../i18n";
import type { NotificationDto } from "../api/notificationsApi";

const TITLE_KEY_METADATA = "i18n.titleKey";
const CONTENT_KEY_METADATA = "i18n.contentKey";
const PARAMETER_PREFIX = "i18n.param.";
const UNKNOWN_GEO_LABEL = "Unknown";
const UNKNOWN_LOCATION_LABEL = "unknown location";
const LOCAL_NETWORK_LOCATION_LABEL = "local network";

export interface RenderedNotificationText {
  title: string;
  content: string | null;
}

const parseIpv4Address = (value: string): number[] | null => {
  const parts = value.split(".");
  if (parts.length !== 4) return null;

  const bytes = parts.map((part) => {
    if (!/^\d{1,3}$/.test(part)) return Number.NaN;
    return Number(part);
  });

  return bytes.every((byte) => Number.isInteger(byte) && byte >= 0 && byte <= 255)
    ? bytes
    : null;
};

const isLocalNetworkIp = (value: string | undefined): boolean => {
  const normalized = value?.trim().toLowerCase().replace(/^\[|\]$/g, "");
  if (!normalized) return false;

  const ipv4Value = normalized.startsWith("::ffff:")
    ? normalized.slice("::ffff:".length)
    : normalized;
  const ipv4 = parseIpv4Address(ipv4Value);
  if (ipv4) {
    return (
      ipv4[0] === 10 ||
      ipv4[0] === 127 ||
      (ipv4[0] === 172 && ipv4[1] >= 16 && ipv4[1] <= 31) ||
      (ipv4[0] === 192 && ipv4[1] === 168) ||
      (ipv4[0] === 169 && ipv4[1] === 254)
    );
  }

  return (
    normalized === "::1" ||
    normalized.startsWith("fe80:") ||
    normalized.startsWith("fc") ||
    normalized.startsWith("fd")
  );
};

const isKnownGeoField = (value: string | undefined): value is string =>
  Boolean(value?.trim()) &&
  value!.trim().toLowerCase() !== UNKNOWN_GEO_LABEL.toLowerCase();

const deriveLocationParam = (params: Record<string, string>): string => {
  if (params.location?.trim()) {
    return params.location;
  }

  if (isLocalNetworkIp(params.ip)) {
    return LOCAL_NETWORK_LOCATION_LABEL;
  }

  const parts = [params.city, params.region, params.country]
    .filter(isKnownGeoField)
    .map((part) => part.trim());

  return parts.length > 0 ? parts.join(", ") : UNKNOWN_LOCATION_LABEL;
};

const collectTemplateParams = (
  metadata: Record<string, string>,
): Record<string, string> => {
  const params: Record<string, string> = {};

  for (const [key, value] of Object.entries(metadata)) {
    if (key === TITLE_KEY_METADATA || key === CONTENT_KEY_METADATA) continue;

    const paramKey = key.startsWith(PARAMETER_PREFIX)
      ? key.slice(PARAMETER_PREFIX.length)
      : key;
    params[paramKey] = value;
  }

  params.location = deriveLocationParam(params);

  return params;
};

const renderTemplate = (
  key: string | undefined,
  metadata: Record<string, string>,
  t: TFunction,
): string | null => {
  if (!key || !i18n.exists(key)) {
    return null;
  }

  return String(t(key, collectTemplateParams(metadata)));
};

export const renderNotificationText = (
  notification: NotificationDto,
  t: TFunction,
): RenderedNotificationText => {
  const metadata = notification.metadata ?? {};

  return {
    title:
      renderTemplate(metadata[TITLE_KEY_METADATA], metadata, t) ??
      notification.title,
    content:
      renderTemplate(metadata[CONTENT_KEY_METADATA], metadata, t) ??
      notification.content,
  };
};
