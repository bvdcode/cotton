import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import type { NodeContentDto } from "../api/nodesApi";
import { NODES_STORAGE_KEY } from "../config/storageKeys";
import { applyDisplayMetaToFiles } from "../crypto/displayMeta";
import { resetNodesActionsInternals } from "./nodesActionInternals";

import type { NodesState } from "./nodesStoreTypes";
import {
  buildPersistedContentSnapshot,
  dropAncestorCachesAffectedByMove,
  safeSessionStorage,
} from "./nodesPersistence";

export const useNodesStore = create<NodesState>()(
  persist(
    (set, get) => ({
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
          const ancestors = prev.ancestors.some(
            (node) => node.id === updated.id,
          )
            ? prev.ancestors.map((node) =>
                node.id === updated.id ? updated : node,
              )
            : prev.ancestors;

          let contentChanged = false;
          const contentByNodeId: Record<string, NodeContentDto | undefined> =
            {};
          for (const [nodeId, content] of Object.entries(
            prev.contentByNodeId,
          )) {
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

      moveFolderInCache: (updated, sourceParentId, targetParentId) => {
        set((prev) => {
          let contentChanged = false;
          const contentByNodeId = { ...prev.contentByNodeId };

          for (const [nodeId, content] of Object.entries(
            prev.contentByNodeId,
          )) {
            if (!content) continue;

            if (nodeId === targetParentId) {
              const nodes = [
                ...content.nodes.filter((node) => node.id !== updated.id),
                updated,
              ];
              contentByNodeId[nodeId] = { ...content, nodes };
              contentChanged = true;
              continue;
            }

            if (nodeId === sourceParentId) {
              const nodes = content.nodes.filter(
                (node) => node.id !== updated.id,
              );
              if (nodes.length !== content.nodes.length) {
                contentByNodeId[nodeId] = { ...content, nodes };
                contentChanged = true;
              }
              continue;
            }

            if (content.nodes.some((node) => node.id === updated.id)) {
              contentByNodeId[nodeId] = {
                ...content,
                nodes: content.nodes.map((node) =>
                  node.id === updated.id ? updated : node,
                ),
              };
              contentChanged = true;
            }
          }

          const now = Date.now();
          return {
            currentNode:
              prev.currentNode?.id === updated.id ? updated : prev.currentNode,
            ancestors: prev.ancestors.map((node) =>
              node.id === updated.id ? updated : node,
            ),
            ancestorsByNodeId: dropAncestorCachesAffectedByMove(
              prev.ancestorsByNodeId,
              updated.id,
            ),
            contentByNodeId: contentChanged
              ? contentByNodeId
              : prev.contentByNodeId,
            lastUpdatedByNodeId: {
              ...prev.lastUpdatedByNodeId,
              [sourceParentId]: now,
              [targetParentId]: now,
            },
          };
        });
      },

      moveFileInCache: (updated, sourceParentId, targetParentId) => {
        set((prev) => {
          let contentChanged = false;
          const contentByNodeId = { ...prev.contentByNodeId };

          for (const [nodeId, content] of Object.entries(
            prev.contentByNodeId,
          )) {
            if (!content) continue;

            if (nodeId === targetParentId) {
              const files = [
                ...content.files.filter((file) => file.id !== updated.id),
                updated,
              ];
              contentByNodeId[nodeId] = { ...content, files };
              contentChanged = true;
              continue;
            }

            if (nodeId === sourceParentId) {
              const files = content.files.filter(
                (file) => file.id !== updated.id,
              );
              if (files.length !== content.files.length) {
                contentByNodeId[nodeId] = { ...content, files };
                contentChanged = true;
              }
              continue;
            }

            if (content.files.some((file) => file.id === updated.id)) {
              contentByNodeId[nodeId] = {
                ...content,
                files: content.files.map((file) =>
                  file.id === updated.id ? updated : file,
                ),
              };
              contentChanged = true;
            }
          }

          const now = Date.now();
          return {
            contentByNodeId: contentChanged
              ? contentByNodeId
              : prev.contentByNodeId,
            lastUpdatedByNodeId: {
              ...prev.lastUpdatedByNodeId,
              [sourceParentId]: now,
              [targetParentId]: now,
            },
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

      updateFileInCache: (parentNodeId, file) => {
        set((prev) => {
          const existing = prev.contentByNodeId[parentNodeId];
          if (!existing) return {};

          const current = existing.files.find((item) => item.id === file.id);
          if (!current) return {};
          if (current === file) return {};

          return {
            contentByNodeId: {
              ...prev.contentByNodeId,
              [parentNodeId]: {
                ...existing,
                files: existing.files.map((item) =>
                  item.id === file.id ? file : item,
                ),
              },
            },
          };
        });
      },

      upsertFileInCache: (parentNodeId, file) => {
        let updated = false;
        set((prev) => {
          const existing = prev.contentByNodeId[parentNodeId];
          if (!existing) return {};

          updated = true;
          const hasFile = existing.files.some((item) => item.id === file.id);
          const files = hasFile
            ? existing.files.map((item) => (item.id === file.id ? file : item))
            : [...existing.files, file];

          return {
            contentByNodeId: {
              ...prev.contentByNodeId,
              [parentNodeId]: {
                ...existing,
                files,
              },
            },
            lastUpdatedByNodeId: {
              ...prev.lastUpdatedByNodeId,
              [parentNodeId]: Date.now(),
            },
          };
        });
        return updated;
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
        let updated = false;
        set((prev) => {
          const existing = prev.contentByNodeId[parentNodeId];
          if (!existing) return {};

          const current = existing.files.find((f) => f.id === fileId);
          if (!current) return {};
          if (current.previewHashEncryptedHex === previewHashEncryptedHex) {
            return {};
          }

          updated = true;
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
        return updated;
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

      refreshCachedFileDisplayMetadata: async () => {
        const snapshots = Object.entries(get().contentByNodeId)
          .filter(
            (entry): entry is [string, NodeContentDto] =>
              entry[1] !== undefined,
          )
          .map(([nodeId, content]) => ({ nodeId, content }));

        if (snapshots.length === 0) {
          return;
        }

        const decorated = await Promise.all(
          snapshots.map(async ({ nodeId, content }) => ({
            nodeId,
            content,
            files: await applyDisplayMetaToFiles(content.files),
          })),
        );

        set((prev) => {
          let changed = false;
          const contentByNodeId = { ...prev.contentByNodeId };

          for (const item of decorated) {
            const current = prev.contentByNodeId[item.nodeId];
            if (current !== item.content || item.files === item.content.files) {
              continue;
            }

            contentByNodeId[item.nodeId] = {
              ...item.content,
              files: item.files,
            };
            changed = true;
          }

          return changed ? { contentByNodeId } : {};
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
