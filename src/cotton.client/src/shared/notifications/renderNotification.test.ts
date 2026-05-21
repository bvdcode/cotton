import { describe, expect, it } from "vitest";
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
