import { useEffect, useMemo, useRef } from "react";
import { eventHub } from "../../../shared/signalr";
import { useAuth } from "../../../features/auth";
import type { JsonValue } from "../../../shared/types/json";

interface UseFilesRealtimeEventsOptions {
  nodeId: string | null;
  onInvalidate: () => void;
  onPreviewGenerated?: (nodeFileId: string, encryptedFilePreviewHashHex: string) => void;
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

const PREVIEW_GENERATED_METHOD = "PreviewGenerated";

const isPreviewGeneratedArgs = (
  args: JsonValue[],
): args is [string, string, string] => {
  const nodeId = args[0];
  const nodeFileId = args[1];
  const hex = args[2];
  return (
    typeof nodeId === "string" &&
    typeof nodeFileId === "string" &&
    typeof hex === "string"
  );
};

export function useFilesRealtimeEvents({
  nodeId,
  onInvalidate,
  onPreviewGenerated,
}: UseFilesRealtimeEventsOptions): void {
  const { isAuthenticated } = useAuth();

  const onInvalidateRef = useRef(onInvalidate);
  useEffect(() => {
    onInvalidateRef.current = onInvalidate;
  }, [onInvalidate]);

  const nodeIdRef = useRef<string | null>(nodeId);
  useEffect(() => {
    nodeIdRef.current = nodeId;
  }, [nodeId]);

  const onPreviewGeneratedRef = useRef(onPreviewGenerated);
  useEffect(() => {
    onPreviewGeneratedRef.current = onPreviewGenerated;
  }, [onPreviewGenerated]);

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
      eventHub.on(method, () => {
        scheduleInvalidate();
      }),
    );

    const unsubscribePreviewGenerated = eventHub.on(
      PREVIEW_GENERATED_METHOD,
      (...args: JsonValue[]) => {
        if (!isPreviewGeneratedArgs(args)) {
          return;
        }

        const [eventNodeId, nodeFileId, previewHashHex] = args;
        if (!nodeIdRef.current || nodeIdRef.current !== eventNodeId) {
          return;
        }

        const handler = onPreviewGeneratedRef.current;
        if (handler) {
          handler(nodeFileId, previewHashHex);
        } else {
          scheduleInvalidate();
        }
      },
    );

    return () => {
      if (scheduledRef.current !== null) {
        window.clearTimeout(scheduledRef.current);
        scheduledRef.current = null;
      }

      for (const unsubscribe of unsubscribes) {
        unsubscribe();
      }

      unsubscribePreviewGenerated();
    };
  }, [isAuthenticated, scheduleInvalidate]);
}
