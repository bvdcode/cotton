import type { NodeDto } from "../api/layoutsApi";
import { isAxiosError } from "../api/httpClient";
import { useNodesStore } from "../store/nodesStore";
import {
  getFolderEncryptionPolicyStateFromParentResolver,
  isFileEncrypted,
} from "../crypto";
import type { MoveClipboardItem } from "../store/moveClipboardStore";
import { isRecord } from "../utils/typeGuards";

export const extractErrorMessage = (error: unknown): string | null => {
  if (!isAxiosError(error)) return null;
  const data = error.response?.data;
  if (isRecord(data)) {
    if (typeof data.message === "string" && data.message.length > 0) {
      return data.message;
    }
  }
  return null;
};

export const findCachedNode = (nodeId: string): NodeDto | null => {
  const state = useNodesStore.getState();

  if (state.currentNode?.id === nodeId) {
    return state.currentNode;
  }

  const ancestor = state.ancestors.find((node) => node.id === nodeId);
  if (ancestor) {
    return ancestor;
  }

  for (const content of Object.values(state.contentByNodeId)) {
    const node = content?.nodes.find((item) => item.id === nodeId);
    if (node) {
      return node;
    }
  }

  return null;
};

export const getCachedFolderEncryptionPolicyEnabled = (
  nodeId: string,
): boolean => {
  const node = findCachedNode(nodeId);
  if (!node) return false;

  return getFolderEncryptionPolicyStateFromParentResolver(node, findCachedNode)
    .effectiveEnabled;
};

export const needsEncryptionAfterMove = (item: MoveClipboardItem): boolean =>
  item.kind === "file" &&
  item.file !== undefined &&
  !isFileEncrypted(item.file.metadata);

export const needsDecryptionAfterMove = (item: MoveClipboardItem): boolean =>
  item.kind === "file" &&
  item.file !== undefined &&
  isFileEncrypted(item.file.metadata);
