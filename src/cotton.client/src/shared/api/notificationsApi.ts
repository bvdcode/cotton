import { httpClient } from "./httpClient";
import type { NotificationDto } from "../types/notification";

interface NotificationsPage {
  data: NotificationDto[];
  totalCount: number;
}

type JsonObject = { [key: string]: JsonValue };

type JsonValue = null | boolean | number | string | JsonValue[] | JsonObject;

const isRecord = (value: JsonValue): value is JsonObject =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const isNotificationDto = (
  value: JsonValue,
): value is NotificationDto & JsonObject => {
  if (!isRecord(value)) return false;

  const id = value.id;
  const createdAt = value.createdAt;
  const updatedAt = value.updatedAt;
  const userId = value.userId;
  const title = value.title;
  const content = value.content;
  const readAt = value.readAt;

  const hasBase =
    typeof id === "string" &&
    typeof createdAt === "string" &&
    typeof updatedAt === "string";
  if (!hasBase) return false;

  if (typeof userId !== "string") return false;
  if (typeof title !== "string") return false;

  const contentOk = content === null || typeof content === "string";
  const readAtOk = readAt === null || typeof readAt === "string";

  return contentOk && readAtOk;
};

const extractNotifications = (payload: JsonValue): NotificationDto[] => {
  if (Array.isArray(payload)) {
    return payload.filter(isNotificationDto);
  }
  if (isRecord(payload)) {
    const candidates: JsonValue[] = [];
    candidates.push(payload.data ?? null);
    candidates.push(payload.notifications ?? null);
    candidates.push(payload.items ?? null);
    candidates.push(payload.results ?? null);

    for (const candidate of candidates) {
      if (Array.isArray(candidate)) {
        const list = candidate.filter(isNotificationDto);
        if (list.length > 0) return list;
      }
    }
  }
  return [];
};

export const notificationsApi = {
  list: async (page = 1, pageSize = 20): Promise<NotificationsPage> => {
    const response = await httpClient.get<JsonValue>("/notifications", {
      params: { page, pageSize },
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
