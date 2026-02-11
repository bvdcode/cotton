import { useEffect } from "react";
import { eventHub } from "../../shared/signalr";
import { useNotificationsStore } from "../../shared/store/notificationsStore";
import type { NotificationDto } from "../../shared/types/notification";
import { useAuth } from "../auth";

const HUB_METHOD = "OnNotificationReceived";

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const isNotificationDto = (value: unknown): value is NotificationDto => {
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

export function useEventHub() {
  const { isAuthenticated } = useAuth();
  const prependNotification = useNotificationsStore(
    (s) => s.prependNotification,
  );
  const fetchUnreadCount = useNotificationsStore((s) => s.fetchUnreadCount);

  useEffect(() => {
    if (!isAuthenticated) return;

    eventHub.start().catch(() => {
      // connection will retry automatically
    });

    const unsubscribe = eventHub.on(HUB_METHOD, (...args) => {
      const first = args[0];
      if (isNotificationDto(first)) {
        prependNotification(first);
        fetchUnreadCount();
      }
    });

    return () => {
      unsubscribe();
      eventHub.dispose();
    };
  }, [isAuthenticated, prependNotification, fetchUnreadCount]);
}
