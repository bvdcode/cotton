import { useEffect } from "react";
import { eventHub } from "../../shared/signalr";
import { useNotificationsStore } from "../../shared/store/notificationsStore";
import { selectNotificationSoundEnabled, useUserPreferencesStore } from "../../shared/store/userPreferencesStore";
import type { JsonValue } from "../../shared/types/json";
import { isNotificationDto } from "../../shared/types/notificationGuards";
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

export function useEventHub() {
  const { isAuthenticated } = useAuth();
  const prependNotification = useNotificationsStore(
    (s) => s.prependNotification,
  );
  const fetchUnreadCount = useNotificationsStore((s) => s.fetchUnreadCount);
  const soundEnabled = useUserPreferencesStore(selectNotificationSoundEnabled);

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
  }, [isAuthenticated, prependNotification, fetchUnreadCount, soundEnabled]);
}
