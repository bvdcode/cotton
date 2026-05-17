import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import type { NodeContentDto } from "../api/nodesApi";
import type { NodeDto } from "../api/layoutsApi";
import { NODES_STORAGE_KEY } from "../config/storageKeys";
import { resetNodesActionsInternals } from "./nodesActionInternals";

type NodesState = {
  cacheOwnerUserId: string | null;
  currentNode: NodeDto | null;
  ancestors: NodeDto[];
  contentByNodeId: Record<string, NodeContentDto | undefined>;
  ancestorsByNodeId: Record<string, NodeDto[] | undefined>;
  rootNodeId: string | null;
  loading: boolean;
  error: string | null;
  lastUpdatedByNodeId: Record<string, number | undefined>;
  updateNode: (updated: NodeDto) => void;
  addFolderToCache: (parentNodeId: string, folder: NodeDto) => void;
  optimisticRenameFile: (parentNodeId: string, fileId: string, newName: string) => void;
  optimisticSetFilePreviewHash: (
    parentNodeId: string,
    fileId: string,
    previewHashEncryptedHex: string,
  ) => void;
  optimisticDeleteFile: (parentNodeId: string, fileId: string) => void;
  reset: (cacheOwnerUserId?: string | null) => void;
};

// Folders larger than this are not persisted to sessionStorage to avoid
// exceeding the roughly 5 MB browser quota; they get refetched on reload.
const MAX_PERSISTED_NODE_CONTENT_ITEMS = 10000;

function buildPersistedContentSnapshot(state: {
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
        contentByNodeId[nodeId] = content;
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

const safeSessionStorage = {
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

      console.warn(
        `[nodesStore] sessionStorage quota exceeded for "${key}" (${value.length} chars). Skipping persistence.`,
      );
    }
  },
};

export const useNodesStore = create<NodesState>()(
  persist(
    (set) => ({
      cacheOwnerUserId: null,
      currentNode: null,
      ancestors: [],
      contentByNodeId: {},
      ancestorsByNodeId: {},
      rootNodeId: null,
      loading: false,
      error: null,
      lastUpdatedByNodeId: {},

      updateNode: (updated) => {
        set((prev) => {
          const currentNode =
            prev.currentNode?.id === updated.id ? updated : prev.currentNode;
          const ancestors = prev.ancestors.some((node) => node.id === updated.id)
            ? prev.ancestors.map((node) =>
                node.id === updated.id ? updated : node,
              )
            : prev.ancestors;

          let contentChanged = false;
          const contentByNodeId: Record<
            string,
            NodeContentDto | undefined
          > = {};
          for (const [nodeId, content] of Object.entries(prev.contentByNodeId)) {
            if (!content) {
              contentByNodeId[nodeId] = content;
              continue;
            }

            if (!content.nodes.some((node) => node.id === updated.id)) {
              contentByNodeId[nodeId] = content;
              continue;
            }

            contentChanged = true;
            contentByNodeId[nodeId] = {
              ...content,
              nodes: content.nodes.map((node) =>
                node.id === updated.id ? updated : node,
              ),
            };
          }

          return {
            currentNode,
            ancestors,
            contentByNodeId: contentChanged
              ? contentByNodeId
              : prev.contentByNodeId,
          };
        });
      },

      addFolderToCache: (parentNodeId, folder) => {
        set((prev) => {
          const existing = prev.contentByNodeId[parentNodeId];
          if (!existing) return {};
          if (existing.nodes.some((n) => n.id === folder.id)) return {};

          return {
            contentByNodeId: {
              ...prev.contentByNodeId,
              [parentNodeId]: {
                ...existing,
                nodes: [...existing.nodes, folder],
              },
            },
          };
        });
      },

      optimisticRenameFile: (parentNodeId, fileId, newName) => {
        set((prev) => {
          const existing = prev.contentByNodeId[parentNodeId];
          if (!existing) return {};

          return {
            contentByNodeId: {
              ...prev.contentByNodeId,
              [parentNodeId]: {
                ...existing,
                files: existing.files.map((f) =>
                  f.id === fileId ? { ...f, name: newName } : f,
                ),
              },
            },
          };
        });
      },

      optimisticSetFilePreviewHash: (
        parentNodeId,
        fileId,
        previewHashEncryptedHex,
      ) => {
        set((prev) => {
          const existing = prev.contentByNodeId[parentNodeId];
          if (!existing) return {};

          const current = existing.files.find((f) => f.id === fileId);
          if (!current) return {};
          if (current.previewHashEncryptedHex === previewHashEncryptedHex) {
            return {};
          }

          return {
            contentByNodeId: {
              ...prev.contentByNodeId,
              [parentNodeId]: {
                ...existing,
                files: existing.files.map((f) =>
                  f.id === fileId ? { ...f, previewHashEncryptedHex } : f,
                ),
              },
            },
          };
        });
      },

      optimisticDeleteFile: (parentNodeId, fileId) => {
        set((prev) => {
          const existing = prev.contentByNodeId[parentNodeId];
          if (!existing) return {};

          return {
            contentByNodeId: {
              ...prev.contentByNodeId,
              [parentNodeId]: {
                ...existing,
                files: existing.files.filter((f) => f.id !== fileId),
              },
            },
          };
        });
      },

      reset: (cacheOwnerUserId) => {
        resetNodesActionsInternals();
        set((prev) => ({
          cacheOwnerUserId: cacheOwnerUserId ?? prev.cacheOwnerUserId,
          currentNode: null,
          ancestors: [],
          contentByNodeId: {},
          ancestorsByNodeId: {},
          rootNodeId: null,
          loading: false,
          error: null,
          lastUpdatedByNodeId: {},
        }));
      },
    }),
    {
      name: NODES_STORAGE_KEY,
      storage: createJSONStorage(() => safeSessionStorage),
      partialize: (state) => {
        const persistedSnapshot = buildPersistedContentSnapshot(state);
        return {
          cacheOwnerUserId: state.cacheOwnerUserId,
          currentNode: state.currentNode,
          ancestors: state.ancestors,
          rootNodeId: state.rootNodeId,
          contentByNodeId: persistedSnapshot.contentByNodeId,
          ancestorsByNodeId: persistedSnapshot.ancestorsByNodeId,
          lastUpdatedByNodeId: persistedSnapshot.lastUpdatedByNodeId,
        };
      },
    },
  ),
);
