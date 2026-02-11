import { httpClient } from "./httpClient";
import type { NotificationDto } from "../types/notification";

interface NotificationsPage {
  data: NotificationDto[];
  totalCount: number;
}

export const notificationsApi = {
  list: async (page = 1, pageSize = 20): Promise<NotificationsPage> => {
    const response = await httpClient.get<NotificationDto[]>("/notifications", {
      params: { page, pageSize },
    });
    const headerRaw = response.headers["x-total-count"];
    const totalCount = headerRaw ? parseInt(headerRaw, 10) : 0;
    return { data: response.data, totalCount };
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
