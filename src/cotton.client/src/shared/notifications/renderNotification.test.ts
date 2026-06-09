import { afterEach, describe, expect, it } from "vitest";
import i18n from "../../i18n";
import type { NotificationDto } from "../api/notificationsApi";
import { renderNotificationText } from "./renderNotification";

const createNotification = (
  metadata?: Record<string, string>,
): NotificationDto => ({
  id: "notification-id",
  createdAt: "2026-05-21T00:00:00Z",
  updatedAt: "2026-05-21T00:00:00Z",
  userId: "user-id",
  title: "Fallback title",
  content: "Fallback content",
  readAt: null,
  metadata,
});

afterEach(async () => {
  await i18n.changeLanguage("en");
});

describe("renderNotificationText", () => {
  it("uses fallback title and content when metadata has no known template", () => {
    const result = renderNotificationText(createNotification(), i18n.t);

    expect(result).toEqual({
      title: "Fallback title",
      content: "Fallback content",
    });
  });

  it("renders localized templates with metadata parameters", () => {
    const result = renderNotificationText(
      createNotification({
        "i18n.titleKey": "notifications:server.storageChunkMissing.title",
        "i18n.contentKey": "notifications:server.storageChunkMissing.content",
        fileName: "report.xlsx",
      }),
      i18n.t,
    );

    expect(result.title).toBe("File data missing from storage");
    expect(result.content).toContain("report.xlsx");
  });

  it("renders notification location from the location metadata parameter", () => {
    const result = renderNotificationText(
      createNotification({
        "i18n.titleKey": "notifications:server.sharedFileDownloaded.title",
        "i18n.contentKey":
          "notifications:server.sharedFileDownloaded.content.withDevice",
        fileName: "report.xlsx",
        device: "Windows PC",
        location: "local network",
        ip: "10.0.0.101",
      }),
      i18n.t,
    );

    expect(result.content).toBe(
      "Your shared file 'report.xlsx' was downloaded from Windows PC in local network (10.0.0.101).",
    );
  });

  it("derives local network location for legacy metadata without location", () => {
    const result = renderNotificationText(
      createNotification({
        "i18n.titleKey": "notifications:server.successfulLogin.title",
        "i18n.contentKey":
          "notifications:server.successfulLogin.content.withDevice",
        device: "Windows PC",
        city: "Unknown",
        region: "Unknown",
        country: "Unknown",
        ip: "10.0.0.101",
      }),
      i18n.t,
    );

    expect(result.content).toBe(
      "Your account was accessed from Windows PC in local network (10.0.0.101). If this wasn't you, please secure your account immediately.",
    );
  });

  it("derives formatted location for legacy metadata with geo fields", () => {
    const result = renderNotificationText(
      createNotification({
        "i18n.titleKey": "notifications:server.successfulLogin.title",
        "i18n.contentKey":
          "notifications:server.successfulLogin.content.withoutDevice",
        city: "Seattle",
        region: "Washington",
        country: "United States",
        ip: "8.8.8.8",
      }),
      i18n.t,
    );

    expect(result.content).toBe(
      "Your account was accessed from Seattle, Washington, United States (8.8.8.8). If this wasn't you, please secure your account immediately.",
    );
  });

  it("localizes derived location labels with the active language", async () => {
    await i18n.changeLanguage("ru");

    const result = renderNotificationText(
      createNotification({
        "i18n.titleKey": "notifications:server.successfulLogin.title",
        "i18n.contentKey":
          "notifications:server.successfulLogin.content.withDevice",
        device: "Windows PC",
        city: "Unknown",
        region: "Unknown",
        country: "Unknown",
        ip: "10.0.0.101",
      }),
      i18n.t,
    );

    expect(result.content).toBe(
      "В ваш аккаунт вошли с Windows PC из локальной сети (10.0.0.101). Если это были не вы, срочно защитите аккаунт.",
    );
  });

  it("keeps fallback text when the template key is unknown", () => {
    const result = renderNotificationText(
      createNotification({
        "i18n.titleKey": "notifications:server.nope.title",
        "i18n.contentKey": "notifications:server.nope.content",
        "i18n.param.fileName": "report.xlsx",
      }),
      i18n.t,
    );

    expect(result).toEqual({
      title: "Fallback title",
      content: "Fallback content",
    });
  });
});
