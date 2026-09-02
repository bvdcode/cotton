import { useEffect, useRef } from "react";
import {
  eventHub,
  HUB_METHODS,
  getHubMethodVariants,
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

const PREVIEW_GENERATED_METHODS = getHubMethodVariants([
  HUB_METHODS.PreviewGenerated,
]);

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
        const updated = handler(nodeFileId, previewHashHex);
        if (updated) {
          return;
        }
      }

      scheduleInvalidate();
    };

    const unsubscribePreviewGenerated = PREVIEW_GENERATED_METHODS.map(
      (method) => eventHub.on(method, handlePreviewGenerated),
    );

    return () => {
      for (const unsubscribe of unsubscribePreviewGenerated) {
        unsubscribe();
      }
    };
  }, [isAuthenticated, scheduleInvalidate]);
}
