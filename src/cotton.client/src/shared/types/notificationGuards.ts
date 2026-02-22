import type { NotificationDto } from "./notification";
import { isJsonObject, type JsonValue } from "./json";

const isRecord = (value: JsonValue): value is Record<string, JsonValue> =>
  isJsonObject(value);

export const isNotificationDto = (
  value: JsonValue,
): value is NotificationDto & Record<string, JsonValue> => {
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

export const extractNotifications = (payload: JsonValue): NotificationDto[] => {
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
