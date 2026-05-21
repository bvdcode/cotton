import type { TFunction } from "i18next";
import i18n from "../../i18n";
import type { NotificationDto } from "../api/notificationsApi";

const TITLE_KEY_METADATA = "i18n.titleKey";
const CONTENT_KEY_METADATA = "i18n.contentKey";
const PARAMETER_PREFIX = "i18n.param.";

export interface RenderedNotificationText {
  title: string;
  content: string | null;
}

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
