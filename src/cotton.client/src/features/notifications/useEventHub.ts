import { useEffect } from "react";
import { eventHub } from "../../shared/signalr";
import { useNotificationsStore } from "../../shared/store/notificationsStore";
import type { NotificationDto } from "../../shared/types/notification";
import { useAuth } from "../auth";

const HUB_METHOD = "OnNotificationReceived";

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
      if (!first || typeof first !== "object") {
        return;
      }
      prependNotification(first as NotificationDto);
      fetchUnreadCount();
    });

    return () => {
      unsubscribe();
      eventHub.dispose();
    };
  }, [isAuthenticated, prependNotification, fetchUnreadCount]);
}
