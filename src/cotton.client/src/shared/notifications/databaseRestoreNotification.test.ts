import { afterEach, expect, it } from "vitest";
import i18n from "../../i18n";
import { supportedLanguages } from "../../locales";
import type { NotificationDto } from "../api/notificationsApi";
import { renderNotificationText } from "./renderNotification";

afterEach(async () => {
  await i18n.changeLanguage("en");
});

const cases = supportedLanguages.flatMap((language) => [
  { language, fallback: false },
  { language, fallback: true },
]);

it.each(cases)(
  "renders the missing vector warning in $language (fallback: $fallback)",
  async ({ language, fallback }) => {
    await i18n.changeLanguage(language);
    const parameters = {
      backupId: "backup-current",
      sourceDatabase: "cotton",
      sourceHost: "localhost",
      sourcePort: "5432",
      serverTimezone: "UTC",
      backupCreatedUtc: "2026-01-01 00:00:00",
      backupCreatedLocal: "2026-01-01 00:00:00",
      restoreCompletedUtcDisplay: "2026-01-02 00:00:00",
      restoreCompletedLocalDisplay: "2026-01-02 00:00:00",
      skippedBackupIds: "backup-skipped",
    };
    const key = fallback
      ? "notifications:server.databaseRestoreCompleted.contentWithFallbackWithoutVector"
      : "notifications:server.databaseRestoreCompleted.contentWithoutVector";
    const notification: NotificationDto = {
      id: "restore-notification",
      userId: "admin-user",
      createdAt: "2026-01-02T00:00:00Z",
      updatedAt: "2026-01-02T00:00:00Z",
      readAt: null,
      title: "Database restored",
      content: null,
      metadata: {
        "i18n.titleKey": "notifications:server.databaseRestoreCompleted.title",
        "i18n.contentKey": key,
        ...parameters,
      },
    };
    const rendered = renderNotificationText(notification, i18n.t);
    const baseKey = fallback
      ? "notifications:server.databaseRestoreCompleted.contentWithFallback"
      : "notifications:server.databaseRestoreCompleted.content";
    expect(rendered.content).toBe(
      `${i18n.t(baseKey, parameters)}\n\n${i18n.t("notifications:server.databaseRestoreCompleted.vectorUnavailable")}`,
    );
    expect(rendered.content).toContain("backup-current");
    expect(rendered.content).toContain("pgvector");
    expect(rendered.content).not.toContain("$t(");
    expect(rendered.content).not.toContain("{{");
    expect(rendered.content?.includes("backup-skipped")).toBe(fallback);
  },
);
