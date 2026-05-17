import { getValidated, httpClient, parseValidated } from "./httpClient";
import {
  notificationListResponseSchema,
  unreadCountResponseSchema,
  type NotificationDto,
} from "./schemas/notification";

export type { NotificationDto } from "./schemas/notification";

interface NotificationsPage {
  data: NotificationDto[];
  totalCount: number;
}

export const notificationsApi = {
  list: async (page = 1, pageSize = 20, unreadOnly = false): Promise<NotificationsPage> => {
    // Gridify: when value is omitted for '=' it matches null/default values.
    // We want unread => readAt is null.
    const filter = unreadOnly ? "readAt=" : undefined;
    const response = await httpClient.get<unknown>("/notifications", {
      params: {
        page,
        pageSize,
        ...(filter ? { filter } : {}),
      },
    });
    const headerRaw = response.headers["x-total-count"];
    const parsedTotalCount =
      typeof headerRaw === "string" ? Number.parseInt(headerRaw, 10) : 0;
    const totalCount = Number.isFinite(parsedTotalCount)
      ? parsedTotalCount
      : 0;

    return {
      data: parseValidated(
        "/notifications",
        response.data,
        notificationListResponseSchema,
      ),
      totalCount,
    };
  },

  markAsRead: async (id: string): Promise<void> => {
    await httpClient.patch(`/notifications/${id}/read`);
  },

  markAllAsRead: async (): Promise<void> => {
    await httpClient.patch("/notifications/mark-all-read");
  },

  getUnreadCount: async (): Promise<number> => {
    const response = await getValidated(
      "/notifications/unread/count",
      unreadCountResponseSchema,
    );
    return response.unreadCount;
  },

  test: async (): Promise<void> => {
    await httpClient.post("/notifications/test");
  },
};
