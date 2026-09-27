import { reportClientWarning } from "@shared/utils/clientDiagnostics";
import type { NodeContentDto } from "../api/nodesApi";
import type { NodeDto } from "../api/layoutsApi";
import { toPersistableFileDisplayMetadata } from "../crypto/displayMeta";

// Folders larger than this are not persisted to sessionStorage to avoid
// exceeding the roughly 5 MB browser quota; they get refetched on reload.
const MAX_PERSISTED_NODE_CONTENT_ITEMS = 10000;

export function buildPersistedContentSnapshot(state: {
  rootNodeId: string | null;
  currentNode: NodeDto | null;
  ancestors: NodeDto[];
  contentByNodeId: Record<string, NodeContentDto | undefined>;
  ancestorsByNodeId: Record<string, NodeDto[] | undefined>;
  lastUpdatedByNodeId: Record<string, number | undefined>;
}) {
  const keepNodeIds = new Set<string>();

  if (state.rootNodeId) {
    keepNodeIds.add(state.rootNodeId);
  }

  if (state.currentNode?.id) {
    keepNodeIds.add(state.currentNode.id);
  }

  for (const ancestor of state.ancestors) {
    keepNodeIds.add(ancestor.id);
  }

  const contentByNodeId: Record<string, NodeContentDto | undefined> = {};
  const ancestorsByNodeId: Record<string, NodeDto[] | undefined> = {};
  const lastUpdatedByNodeId: Record<string, number | undefined> = {};

  for (const nodeId of keepNodeIds) {
    const content = state.contentByNodeId[nodeId];
    if (content) {
      const itemCount = content.nodes.length + content.files.length;
      if (itemCount <= MAX_PERSISTED_NODE_CONTENT_ITEMS) {
        contentByNodeId[nodeId] = {
          ...content,
          files: content.files
            .map(toPersistableFileDisplayMetadata)
            .filter((file) => file !== null),
        };
      }
    }

    ancestorsByNodeId[nodeId] = state.ancestorsByNodeId[nodeId];
    lastUpdatedByNodeId[nodeId] = state.lastUpdatedByNodeId[nodeId];
  }

  return {
    contentByNodeId,
    ancestorsByNodeId,
    lastUpdatedByNodeId,
  };
}

export function dropAncestorCachesAffectedByMove(
  ancestorsByNodeId: Record<string, NodeDto[] | undefined>,
  movedNodeId: string,
): Record<string, NodeDto[] | undefined> {
  let changed = false;
  const next = { ...ancestorsByNodeId };

  for (const [nodeId, ancestors] of Object.entries(ancestorsByNodeId)) {
    if (
      nodeId === movedNodeId ||
      ancestors?.some((node) => node.id === movedNodeId)
    ) {
      delete next[nodeId];
      changed = true;
    }
  }

  return changed ? next : ancestorsByNodeId;
}

export const safeSessionStorage = {
  getItem: (key: string) => sessionStorage.getItem(key),
  removeItem: (key: string) => sessionStorage.removeItem(key),
  setItem: (key: string, value: string) => {
    try {
      sessionStorage.setItem(key, value);
    } catch (error) {
      const isQuota =
        error instanceof DOMException &&
        (error.name === "QuotaExceededError" ||
          error.name === "NS_ERROR_DOM_QUOTA_REACHED");
      if (!isQuota) throw error;

      try {
        sessionStorage.removeItem(key);
      } catch {
        // ignore
      }

      reportClientWarning(
        `[nodesStore] sessionStorage quota exceeded for "${key}" (${value.length} chars). Skipping persistence.`,
      );
    }
  },
};
