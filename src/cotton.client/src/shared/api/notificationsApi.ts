import { httpClient } from "./httpClient";
import type { NotificationDto } from "../types/notification";
import type { JsonValue } from "../types/json";
import { extractNotifications } from "../types/notificationGuards";

interface NotificationsPage {
  data: NotificationDto[];
  totalCount: number;
}

export const notificationsApi = {
  list: async (page = 1, pageSize = 20, unreadOnly = false): Promise<NotificationsPage> => {
    // Gridify: when value is omitted for '=' it matches null/default values.
    // We want unread => readAt is null.
    const filter = unreadOnly ? "readAt=" : undefined;
    const response = await httpClient.get<JsonValue>("/notifications", {
      params: {
        page,
        pageSize,
        ...(filter ? { filter } : {}),
      },
    });
    const headerRaw = response.headers["x-total-count"];
    const totalCount = headerRaw ? parseInt(headerRaw, 10) : 0;
    return { data: extractNotifications(response.data), totalCount };
  },

  markAsRead: async (id: string): Promise<void> => {
    await httpClient.patch(`/notifications/${id}/read`);
  },

  markAllAsRead: async (): Promise<void> => {
    await httpClient.patch("/notifications/mark-all-read");
  },

  getUnreadCount: async (): Promise<number> => {
    const response = await httpClient.get<{ unreadCount: number }>(
      "/notifications/unread/count",
    );
    return response.data.unreadCount;
  },

  test: async (): Promise<void> => {
    await httpClient.post("/notifications/test");
  },
};
