import { create } from "zustand";
import { queryClient } from "../api/queries/queryClient";
import { queryKeys } from "../api/queries/queryKeys";
import {
  clearNodeQueries,
  type NodeNavigation,
} from "../api/queries/nodeNavigation";
import {
  addCachedFolder,
  upsertCachedFile,
  updateCachedListings,
  invalidateCachedFolder,
  refreshCachedDisplayMetadata,
  updateCachedFile,
  updateCachedNode,
} from "../api/queries/nodeCache";
import type { NodesState } from "./nodesStoreTypes";

export const useNodesStore = create<NodesState>()((set, get) => ({
  cacheOwnerUserId: null,
  currentNodeId: null,
  rootNodeId: null,
  navigationVersion: 0,
  loading: false,
  error: null,
  updateNode: (node) => {
    const owner = get().cacheOwnerUserId;
    if (owner) {
      updateCachedNode(queryClient, owner, node);
    }
  },
  moveFolderInCache: (node, sourceParentId, targetParentId) => {
    const owner = get().cacheOwnerUserId;
    if (!owner) {
      return;
    }
    updateCachedNode(queryClient, owner, node);
    if (sourceParentId !== targetParentId) {
      updateCachedListings(queryClient, owner, (page) => {
        if (
          page.content.node.id !== sourceParentId ||
          !page.content.nodes.some((item) => item.id === node.id)
        ) {
          return page;
        }
        return {
          ...page,
          totalCount: page.totalCount - 1,
          content: {
            ...page.content,
            nodes: page.content.nodes.filter((item) => item.id !== node.id),
          },
        };
      });
      addCachedFolder(queryClient, owner, targetParentId, node);
    }
    invalidateCachedFolder(queryClient, sourceParentId, owner);
    invalidateCachedFolder(queryClient, targetParentId, owner);
    const views = queryClient.getQueriesData<NodeNavigation>({
      queryKey: queryKeys.nodeNavigation.all(),
    });
    for (const [key, view] of views) {
      if (
        key[2] === owner &&
        (view?.node.id === node.id ||
          view?.ancestors.some((ancestor) => ancestor.id === node.id))
      ) {
        void queryClient.invalidateQueries({ queryKey: key, exact: true });
      }
    }
  },
  moveFileInCache: (file, sourceParentId, targetParentId) => {
    const owner = get().cacheOwnerUserId;
    if (!owner) {
      return;
    }
    updateCachedFile(queryClient, owner, sourceParentId, file.id, () =>
      sourceParentId === targetParentId ? file : null,
    );
    upsertCachedFile(queryClient, owner, targetParentId, file);
    invalidateCachedFolder(queryClient, sourceParentId, owner);
    invalidateCachedFolder(queryClient, targetParentId, owner);
  },
  addFolderToCache: (parentId, node) => {
    const owner = get().cacheOwnerUserId;
    if (owner) {
      addCachedFolder(queryClient, owner, parentId, node);
      invalidateCachedFolder(queryClient, parentId, owner);
    }
  },
  updateFileInCache: (parentId, file) => {
    const owner = get().cacheOwnerUserId;
    if (owner) {
      updateCachedFile(queryClient, owner, parentId, file.id, () => file);
    }
  },
  upsertFileInCache: (parentId, file) => {
    const owner = get().cacheOwnerUserId;
    if (!owner) {
      return false;
    }
    const updated = upsertCachedFile(queryClient, owner, parentId, file);
    invalidateCachedFolder(queryClient, parentId, owner);
    return updated;
  },
  optimisticRenameFile: (parentId, fileId, name) => {
    const owner = get().cacheOwnerUserId;
    if (owner) {
      updateCachedFile(queryClient, owner, parentId, fileId, (file) => ({
        ...file,
        name,
      }));
    }
  },
  optimisticSetFilePreviewHash: (parentId, fileId, previewHashEncryptedHex) => {
    const owner = get().cacheOwnerUserId;
    return owner
      ? updateCachedFile(queryClient, owner, parentId, fileId, (file) =>
          file.previewHashEncryptedHex === previewHashEncryptedHex
            ? file
            : { ...file, previewHashEncryptedHex },
        )
      : false;
  },
  optimisticDeleteFile: (parentId, fileId) => {
    const owner = get().cacheOwnerUserId;
    if (owner) {
      updateCachedFile(queryClient, owner, parentId, fileId, () => null);
    }
  },
  refreshCachedFileDisplayMetadata: async () => {
    const owner = get().cacheOwnerUserId;
    if (owner) {
      await refreshCachedDisplayMetadata(queryClient, owner);
    }
  },
  reset: (owner) => {
    clearNodeQueries(queryClient);
    set((state) => ({
      cacheOwnerUserId: owner === undefined ? state.cacheOwnerUserId : owner,
      currentNodeId: null,
      rootNodeId: null,
      navigationVersion: state.navigationVersion + 1,
      loading: false,
      error: null,
    }));
  },
}));
