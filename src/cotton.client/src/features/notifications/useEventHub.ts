import { useEffect } from "react";
import { eventHub } from "../../shared/signalr";
import { useNotificationsStore } from "../../shared/store/notificationsStore";
import { usePreferencesStore } from "../../shared/store/preferencesStore";
import type { NotificationDto } from "../../shared/types/notification";
import { isJsonObject, type JsonValue } from "../../shared/types/json";
import { useAuth } from "../auth";

const playNotificationSound = () => {
  try {
    const audio = new Audio("/assets/sounds/notification-3.mp3");
    audio.volume = 0.5;
    audio.play().catch(() => {
      // audio playback may fail due to browser autoplay policy
    });
  } catch {
    // audio not available
  }
};

const HUB_METHOD = "OnNotificationReceived";

const isRecord = (value: JsonValue) => isJsonObject(value);

const isNotificationDto = (
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

export function useEventHub() {
  const { isAuthenticated } = useAuth();
  const prependNotification = useNotificationsStore(
    (s) => s.prependNotification,
  );
  const fetchUnreadCount = useNotificationsStore((s) => s.fetchUnreadCount);
  const soundEnabled = usePreferencesStore(
    (s) => s.notificationPreferences.soundEnabled,
  );

  useEffect(() => {
    if (!isAuthenticated) {
      eventHub.dispose();
      return;
    }

    eventHub.start().catch(() => {
      // connection will retry automatically
    });

    const unsubscribe = eventHub.on(HUB_METHOD, (...args) => {
      const first = (args[0] ?? null) as JsonValue;
      if (isNotificationDto(first)) {
        prependNotification(first);
        fetchUnreadCount();
        if (soundEnabled) {
          playNotificationSound();
        }
      }
    });

    return () => {
      unsubscribe();
    };
  }, [isAuthenticated, prependNotification, fetchUnreadCount]);
}
