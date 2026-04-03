import { useEffect, useMemo, useRef } from "react";
import { eventHub } from "../../../shared/signalr";
import { useAuth } from "../../../features/auth";
import { isJsonObject, type JsonValue } from "../../../shared/types/json";

interface UseFilesRealtimeEventsOptions {
  nodeId: string | null;
  onInvalidate: () => void;
  onPreviewGenerated?: (nodeFileId: string, previewHashEncryptedHex: string) => void;
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
const GUID_REGEX =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const MAX_PAYLOAD_SCAN_DEPTH = 4;

const normalizeKey = (key: string): string =>
  key.replace(/[^a-z]/gi, "").toLowerCase();

const looksLikeNodeRelationKey = (key: string): boolean => {
  const normalized = normalizeKey(key);

  if (
    normalized === "node" ||
    normalized === "parent" ||
    normalized === "folder"
  ) {
    return true;
  }

  return (
    normalized.includes("nodeid") ||
    normalized.includes("parentid") ||
    normalized.includes("folderid") ||
    normalized.includes("sourceid") ||
    normalized.includes("targetid") ||
    normalized.includes("destinationid") ||
    normalized.includes("fromid") ||
    normalized.includes("toid")
  );
};

const isGuid = (value: string): boolean => GUID_REGEX.test(value);

const collectAffectedNodeIdsFromValue = (
  value: JsonValue,
  depth: number,
  relationContext: boolean,
): string[] => {
  if (depth > MAX_PAYLOAD_SCAN_DEPTH) {
    return [];
  }

  if (typeof value === "string") {
    return relationContext && isGuid(value) ? [value] : [];
  }

  if (Array.isArray(value)) {
    return value.flatMap((entry) =>
      collectAffectedNodeIdsFromValue(entry, depth + 1, relationContext),
    );
  }

  if (!isJsonObject(value)) {
    return [];
  }

  const ids: string[] = [];

  for (const [key, nested] of Object.entries(value)) {
    const nextRelationContext = relationContext || looksLikeNodeRelationKey(key);
    ids.push(
      ...collectAffectedNodeIdsFromValue(
        nested,
        depth + 1,
        nextRelationContext,
      ),
    );
  }

  return ids;
};

const collectAffectedNodeIds = (args: JsonValue[]): Set<string> => {
  const ids = new Set<string>();

  for (const arg of args) {
    if (typeof arg === "string" && isGuid(arg)) {
      ids.add(arg);
    }

    const nestedIds = collectAffectedNodeIdsFromValue(arg, 0, false);
    for (const id of nestedIds) {
      ids.add(id);
    }
  }

  return ids;
};

const shouldInvalidateCurrentNode = (
  args: JsonValue[],
  currentNodeId: string | null,
): boolean => {
  if (!currentNodeId) {
    return false;
  }

  const affectedNodeIds = collectAffectedNodeIds(args);
  if (affectedNodeIds.size === 0) {
    // Keep compatibility with events that do not include node identifiers.
    return true;
  }

  return affectedNodeIds.has(currentNodeId);
};

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

    const invalidationMethods = FILES_HUB_METHODS.flatMap((m) => [m, m.toLowerCase()]);
    const unsubscribes = invalidationMethods.map((method) =>
      eventHub.on(method, (...args: JsonValue[]) => {
        if (!shouldInvalidateCurrentNode(args, nodeIdRef.current)) {
          return;
        }

        scheduleInvalidate();
      }),
    );

    const handlePreviewGenerated = (...args: JsonValue[]) => {
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
    };

    const unsubscribePreviewGenerated = eventHub.on(
      PREVIEW_GENERATED_METHOD,
      handlePreviewGenerated,
    );

    const unsubscribePreviewGeneratedLower = eventHub.on(
      PREVIEW_GENERATED_METHOD.toLowerCase(),
      handlePreviewGenerated,
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
      unsubscribePreviewGeneratedLower();
    };
  }, [isAuthenticated, scheduleInvalidate]);
}
