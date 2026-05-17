import { describe, expect, it } from "vitest";
import {
  isNotificationDto,
  notificationListResponseSchema,
  unreadCountResponseSchema,
} from "./notification";

const notification = {
  id: "notification-id",
  createdAt: "2026-05-16T00:00:00Z",
  updatedAt: "2026-05-16T00:00:00Z",
  userId: "user-id",
  title: "Title",
  content: null,
  readAt: null,
};

describe("notification schemas", () => {
  it("accepts a notification dto", () => {
    expect(isNotificationDto(notification)).toBe(true);
  });

  it("rejects invalid notification dto fields", () => {
    expect(isNotificationDto({ ...notification, readAt: 1 })).toBe(false);
  });

  it("unwraps supported notification list envelopes", () => {
    expect(notificationListResponseSchema.parse([notification])).toEqual([
      notification,
    ]);
    expect(
      notificationListResponseSchema.parse({ data: [notification] }),
    ).toEqual([notification]);
    expect(
      notificationListResponseSchema.parse({ notifications: [notification] }),
    ).toEqual([notification]);
    expect(
      notificationListResponseSchema.parse({ items: [notification] }),
    ).toEqual([notification]);
    expect(
      notificationListResponseSchema.parse({ results: [notification] }),
    ).toEqual([notification]);
  });

  it("validates unread count responses", () => {
    expect(unreadCountResponseSchema.parse({ unreadCount: 3 })).toEqual({
      unreadCount: 3,
    });
    expect(() =>
      unreadCountResponseSchema.parse({ unreadCount: "3" }),
    ).toThrow();
  });
});
