import { useEffect, useMemo, useRef } from "react";
import { eventHub } from "../../../shared/signalr";
import type { JsonValue } from "../../../shared/types/json";
import { useAuth } from "../../../features/auth";

interface UseFilesRealtimeEventsOptions {
  onInvalidate: () => void;
}

const FILES_HUB_METHODS = [
  "FileCreated",
  "FileUpdated",
  "FileDeleted",
  "FileMoved",
  "FileRenamed",
  "NodeCreated",
  "NodeDeleted",
  "NodeMoved",
  "NodeRenamed",
] as const;

export function useFilesRealtimeEvents({
  onInvalidate,
}: UseFilesRealtimeEventsOptions): void {
  const { isAuthenticated } = useAuth();

  const onInvalidateRef = useRef(onInvalidate);
  useEffect(() => {
    onInvalidateRef.current = onInvalidate;
  }, [onInvalidate]);

  const scheduledRef = useRef<number | null>(null);

  const scheduleInvalidate = useMemo(() => {
    return (): void => {
      if (scheduledRef.current !== null) {
        return;
      }

      // Coalesce bursts (uploads/renames) into a single refresh.
      scheduledRef.current = window.setTimeout(() => {
        scheduledRef.current = null;
        onInvalidateRef.current();
      }, 250);
    };
  }, []);

  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }

    eventHub.start().catch(() => {
      // connection will retry automatically
    });

    const unsubscribes = FILES_HUB_METHODS.map((method) =>
      eventHub.on(method, (..._args: JsonValue[]) => {
        scheduleInvalidate();
      }),
    );

    return () => {
      if (scheduledRef.current !== null) {
        window.clearTimeout(scheduledRef.current);
        scheduledRef.current = null;
      }

      for (const unsubscribe of unsubscribes) {
        unsubscribe();
      }
    };
  }, [isAuthenticated, scheduleInvalidate]);
}
