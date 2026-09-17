import { useEffect, useRef } from "react";
import {
  HUB_METHODS,
  subscribeToPreviewGenerated,
  type HubMethod,
  type HubMethodOrLower,
  useFileTreeRealtimeInvalidation,
} from "../../../shared/signalr";
import { useAuth } from "../../../features/auth";
import { isJsonObject, type JsonValue } from "../../../shared/types/json";
import { isGuidString } from "../../../shared/utils/guid";

interface UseFilesRealtimeEventsOptions {
  nodeId: string | null;
  onInvalidate: () => void;
  onPreviewGenerated?: (
    nodeFileId: string,
    previewHashEncryptedHex: string,
  ) => boolean;
}

const HUB_METHOD_BY_WIRE_NAME = new Map<string, HubMethod>(
  Object.values(HUB_METHODS).map((method) => [method.toLowerCase(), method]),
);

export const shouldInvalidateCurrentNode = (
  method: HubMethodOrLower,
  args: JsonValue[],
  currentNodeId: string | null,
): boolean => {
  if (!currentNodeId) {
    return false;
  }

  const affectedNodeIds = getAffectedNodeIds(method, args);
  return affectedNodeIds.has(currentNodeId);
};

const getAffectedNodeIds = (
  method: HubMethodOrLower,
  args: JsonValue[],
): Set<string> => {
  const canonicalMethod = HUB_METHOD_BY_WIRE_NAME.get(method.toLowerCase());
  const payload = args[0];
  const affected = new Set<string>();

  if (!canonicalMethod || !isJsonObject(payload)) {
    return affected;
  }

  const addPayloadGuid = (key: string): void => {
    const value = payload[key];
    if (typeof value === "string" && isGuidString(value)) {
      affected.add(value);
    }
  };

  const addNestedGuid = (objectKey: string, nestedKey: string): void => {
    const nested = payload[objectKey];
    if (!isJsonObject(nested)) {
      return;
    }

    const value = nested[nestedKey];
    if (typeof value === "string" && isGuidString(value)) {
      affected.add(value);
    }
  };

  switch (canonicalMethod) {
    case HUB_METHODS.FileCreated:
    case HUB_METHODS.FileUpdated:
    case HUB_METHODS.FileRenamed:
    case HUB_METHODS.FileRestored:
      addPayloadGuid("nodeId");
      break;

    case HUB_METHODS.FileDeleted:
      addPayloadGuid("parentNodeId");
      break;

    case HUB_METHODS.FileMoved:
      addPayloadGuid("oldParentId");
      addPayloadGuid("newParentId");
      addNestedGuid("file", "nodeId");
      break;

    case HUB_METHODS.NodeCreated:
    case HUB_METHODS.NodeMetadataUpdated:
    case HUB_METHODS.NodeRenamed:
    case HUB_METHODS.NodeRestored:
      addPayloadGuid("id");
      addPayloadGuid("parentId");
      break;

    case HUB_METHODS.NodeDeleted:
      addPayloadGuid("nodeId");
      addPayloadGuid("parentNodeId");
      break;

    case HUB_METHODS.NodeMoved:
      addPayloadGuid("oldParentId");
      addPayloadGuid("newParentId");
      addNestedGuid("node", "id");
      addNestedGuid("node", "parentId");
      break;
  }

  return affected;
};

export function useFilesRealtimeEvents({
  nodeId,
  onInvalidate,
  onPreviewGenerated,
}: UseFilesRealtimeEventsOptions): void {
  const { isAuthenticated } = useAuth();

  const nodeIdRef = useRef<string | null>(nodeId);
  useEffect(() => {
    nodeIdRef.current = nodeId;
  }, [nodeId]);

  const onPreviewGeneratedRef = useRef(onPreviewGenerated);
  useEffect(() => {
    onPreviewGeneratedRef.current = onPreviewGenerated;
  }, [onPreviewGenerated]);

  const scheduleInvalidate = useFileTreeRealtimeInvalidation({
    enabled: isAuthenticated,
    onInvalidate,
    shouldInvalidate: (method, args) =>
      shouldInvalidateCurrentNode(method, args, nodeIdRef.current),
  });

  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }

    return subscribeToPreviewGenerated(
      (eventNodeId, nodeFileId, previewHashHex) => {
        if (!nodeIdRef.current || nodeIdRef.current !== eventNodeId) {
          return;
        }

        const handler = onPreviewGeneratedRef.current;
        if (handler && handler(nodeFileId, previewHashHex)) {
          return;
        }

        scheduleInvalidate();
      },
    );
  }, [isAuthenticated, scheduleInvalidate]);
}
