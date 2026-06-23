import { useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  invalidateNotificationQueries,
  prependCachedNotification,
} from "../../shared/api/queries/notifications";
import { notificationSchema } from "../../shared/api/schemas/notification";
import { eventHub, HUB_METHODS } from "../../shared/signalr";
import {
  selectNotificationSoundEnabled,
  useUserPreferencesStore,
} from "../../shared/store/userPreferencesStore";
import { useAuth } from "../auth";

const playNotificationSound = () => {
  try {
    const audio = new Audio("/assets/sounds/notification-6.m4a");
    audio.volume = 0.5;
    audio.play().catch(() => {
      // audio playback may fail due to browser autoplay policy
    });
  } catch {
    // audio not available
  }
};

export function useEventHub() {
  const { isAuthenticated } = useAuth();
  const queryClient = useQueryClient();
  const soundEnabled = useUserPreferencesStore(selectNotificationSoundEnabled);

  useEffect(() => {
    if (!isAuthenticated) {
      eventHub.dispose();
      return;
    }

    const unsubscribeConnected = eventHub.onConnected(() => {
      invalidateNotificationQueries(queryClient);
    });

    eventHub.start().catch(() => {
      // connection will retry automatically
    });

    const unsubscribe = eventHub.on(
      HUB_METHODS.NotificationReceived,
      (...args) => {
        const parsed = notificationSchema.safeParse(args[0] ?? null);
        if (!parsed.success) return;

        prependCachedNotification(queryClient, parsed.data);
        if (soundEnabled) {
          playNotificationSound();
        }
      },
    );

    return () => {
      unsubscribe();
      unsubscribeConnected();
    };
  }, [isAuthenticated, queryClient, soundEnabled]);
}
