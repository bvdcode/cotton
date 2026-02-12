import { create } from "zustand";
import { persist } from "zustand/middleware";
import { nodesApi, type NodeContentDto } from "../api/nodesApi";
import { layoutsApi, type NodeDto } from "../api/layoutsApi";
import { NODES_STORAGE_KEY } from "../config/storageKeys";
import { isAxiosError } from "../api/httpClient";

let rootResolvePromise: Promise<void> | null = null;

type NodesState = {
  currentNode: NodeDto | null;
  ancestors: NodeDto[];
  contentByNodeId: Record<string, NodeContentDto | undefined>;
  ancestorsByNodeId: Record<string, NodeDto[] | undefined>;
  rootNodeId: string | null;

  loading: boolean;
  error: string | null;
  lastUpdatedByNodeId: Record<string, number | undefined>;

  loadRoot: (options?: { force?: boolean; loadChildren?: boolean }) => Promise<NodeDto | null>;
  loadNode: (
    nodeId: string,
    options?: { loadChildren?: boolean; allowRootRecovery?: boolean },
  ) => Promise<void>;
  resolveRootInBackground: (options?: { loadChildren?: boolean }) => void;
  refreshNodeContent: (nodeId: string) => Promise<void>;
  addFolderToCache: (parentNodeId: string, folder: NodeDto) => void;
  createFolder: (parentNodeId: string, name: string) => Promise<NodeDto | null>;
  deleteFolder: (nodeId: string, parentNodeId?: string, skipTrash?: boolean) => Promise<boolean>;
  renameFolder: (
    nodeId: string,
    newName: string,
    parentNodeId?: string,
  ) => Promise<boolean>;
  optimisticRenameFile: (parentNodeId: string, fileId: string, newName: string) => void;
  optimisticDeleteFile: (parentNodeId: string, fileId: string) => void;
  reset: () => void;
};

async function resolveNodeAndAncestors(
  nodeId: string,
  state: Pick<NodesState, "currentNode" | "ancestors" | "ancestorsByNodeId" | "contentByNodeId">,
): Promise<{ node: NodeDto; ancestors: NodeDto[] }> {
  let node: NodeDto | null = null;
  let ancestors = state.ancestorsByNodeId[nodeId];

  // Direct match: resolving the currently persisted node (e.g. home after refresh)
  if (state.currentNode?.id === nodeId) {
    node = state.currentNode;
    if (!ancestors) {
      ancestors = state.ancestors;
    }
  }

  // Check in-memory ancestors (back navigation)
  if (!node) {
    const ancestorIndex = state.ancestors.findIndex((item) => item.id === nodeId);
    if (ancestorIndex >= 0) {
      node = state.ancestors[ancestorIndex];
      if (!ancestors) {
        ancestors = state.ancestors.slice(0, ancestorIndex);
      }
    }
  }

  // Check current node's children (forward navigation)
  if (!node && state.currentNode) {
    const parentContent = state.contentByNodeId[state.currentNode.id];
    node = parentContent?.nodes.find((item) => item.id === nodeId) ?? null;
    if (!ancestors && node && node.parentId === state.currentNode.id) {
      ancestors = [...state.ancestors, state.currentNode];
    }
  }

  // Search all cached content (previously visited node after persistence reload)
  if (!node) {
    for (const content of Object.values(state.contentByNodeId)) {
      if (!content) continue;
      const found = content.nodes.find((n) => n.id === nodeId);
      if (found) {
        node = found;
        break;
      }
    }
  }

  node ??= await nodesApi.getNode(nodeId);
  ancestors ??= await nodesApi.getAncestors(nodeId);

  return { node, ancestors };
}

export const useNodesStore = create<NodesState>()(
  persist(
    (set, get) => {
      const scheduleRootResolve = (options?: { loadChildren?: boolean }): void => {
        // If we don't have a root yet, loadRoot() will resolve it.
        const existingRootId = get().rootNodeId;
        if (!existingRootId) return;

        if (rootResolvePromise) return;

        const loadChildren = options?.loadChildren ?? true;

        rootResolvePromise = (async () => {
          try {
            const root = await layoutsApi.resolve();
            const state = get();

            if (state.rootNodeId === root.id) {
              return;
            }

            const previousRootId = state.rootNodeId;
            set({ rootNodeId: root.id });

            // If the user is currently viewing the root folder, switch to the new root.
            const isViewingRoot =
              state.currentNode == null || state.currentNode.id === previousRootId;
            if (isViewingRoot) {
              await get().loadNode(root.id, {
                loadChildren,
                allowRootRecovery: false,
              });
            }
          } catch (error) {
            // Non-blocking best-effort refresh.
            console.error("Failed to resolve root node in background", error);
          }
        })().finally(() => {
          rootResolvePromise = null;
        });
      };

      return {
  currentNode: null,
  ancestors: [],
  contentByNodeId: {},
  ancestorsByNodeId: {},
  rootNodeId: null,
  loading: false,
  error: null,
  lastUpdatedByNodeId: {},

  loadRoot: async (options) => {
    const force = options?.force ?? false;
    const loadChildren = options?.loadChildren ?? true;
    const state = get();

    if (!force && state.rootNodeId) {
      const hasCachedContent = state.contentByNodeId[state.rootNodeId];
      if (!loadChildren || hasCachedContent) {
        await get().loadNode(state.rootNodeId, { loadChildren });

        // Always try to keep the persisted root in sync with backend.
        scheduleRootResolve({ loadChildren });
        return state.currentNode;
      }
    }

    try {
      const root = await layoutsApi.resolve();
      set({ rootNodeId: root.id });
      await get().loadNode(root.id, { loadChildren });
      return root;
    } catch (error) {
      console.error("Failed to resolve root node", error);
      set({ loading: false, error: "Failed to resolve root node" });
      return null;
    }
  },

  loadNode: async (nodeId, options) => {
    const state = get();
    const loadChildren = options?.loadChildren ?? true;
    const allowRootRecovery = options?.allowRootRecovery ?? true;
    if (state.loading && state.currentNode?.id === nodeId) return;

    const cachedContent = state.contentByNodeId[nodeId];

    // Only show loading spinner when no cached content exists for this node
    if (cachedContent) {
      set({ error: null });
    } else {
      set({ loading: true, error: null });
    }

    try {
      const resolved = await resolveNodeAndAncestors(nodeId, state);
      const content = loadChildren
        ? (cachedContent ?? (await nodesApi.getChildren(nodeId)).content)
        : undefined;

      set((prev) => ({
        currentNode: resolved.node,
        ancestors: resolved.ancestors,
        ancestorsByNodeId: {
          ...prev.ancestorsByNodeId,
          [nodeId]: resolved.ancestors,
        },
        contentByNodeId: loadChildren
          ? {
              ...prev.contentByNodeId,
              [nodeId]: content,
            }
          : prev.contentByNodeId,
        lastUpdatedByNodeId: {
          ...prev.lastUpdatedByNodeId,
          [nodeId]: Date.now(),
        },
        loading: false,
        error: null,
      }));

      // Background refresh when cached content was used
      if (loadChildren && cachedContent) {
        void (async () => {
          try {
            const fresh = await nodesApi.getChildren(nodeId);
            set((prev) => ({
              contentByNodeId: {
                ...prev.contentByNodeId,
                [nodeId]: fresh.content,
              },
              lastUpdatedByNodeId: {
                ...prev.lastUpdatedByNodeId,
                [nodeId]: Date.now(),
              },
            }));
          } catch {
            // Silent: background refresh failure is non-critical
          }
        })();
      }

      // If we're loading the current root, keep it synced in background.
      if (allowRootRecovery && get().rootNodeId === nodeId) {
        scheduleRootResolve({ loadChildren });
      }
    } catch (error) {
      const statusCode = isAxiosError(error) ? error.response?.status : undefined;

      if (
        allowRootRecovery &&
        statusCode === 404 &&
        get().rootNodeId === nodeId
      ) {
        try {
          // Persisted root can become stale after backend data reset.
          // Recover by clearing cached root and resolving a fresh one.
          set({
            rootNodeId: null,
            currentNode: null,
            ancestors: [],
            contentByNodeId: {},
            ancestorsByNodeId: {},
            lastUpdatedByNodeId: {},
          });

          const root = await layoutsApi.resolve();
          set({ rootNodeId: root.id });
          await get().loadNode(root.id, {
            loadChildren,
            allowRootRecovery: false,
          });
          return;
        } catch (recoveryError) {
          console.error("Failed to recover root node", recoveryError);
          set({ loading: false, error: "Failed to resolve root node" });
          return;
        }
      }

      console.error("Failed to load node view", error);
      set({ loading: false, error: "Failed to load folder contents" });
    }
  },

  resolveRootInBackground: (options) => {
    scheduleRootResolve(options);
  },

  refreshNodeContent: async (nodeId) => {
    try {
      const content = await nodesApi.getChildren(nodeId);
      set((prev) => ({
        contentByNodeId: {
          ...prev.contentByNodeId,
          [nodeId]: content.content,
        },
        lastUpdatedByNodeId: {
          ...prev.lastUpdatedByNodeId,
          [nodeId]: Date.now(),
        },
      }));
    } catch (error) {
      console.error("Failed to refresh node content", error);
    }
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

  createFolder: async (parentNodeId, name) => {
    const trimmed = name.trim();
    if (trimmed.length === 0) return null;
    if (get().loading) return null;

    const state = get();
    const currentContent = state.contentByNodeId[parentNodeId];

    // Check for duplicate name (case-insensitive) in cached content
    if (currentContent) {
      const normalizedName = trimmed.toLowerCase();
      const duplicate = currentContent.nodes.find(
        (n) => n.name.toLowerCase() === normalizedName,
      );
      if (duplicate) {
        set({ error: "A folder with this name already exists" });
        return null;
      }
    }

    set({ loading: true, error: null });

    try {
      const created = await nodesApi.createNode({
        parentId: parentNodeId,
        name: trimmed,
      });

      // Optimistic update: immediately add the new folder to local cache
      set((prev) => {
        const existing = prev.contentByNodeId[parentNodeId];
        if (!existing) return { loading: false };

        return {
          contentByNodeId: {
            ...prev.contentByNodeId,
            [parentNodeId]: {
              ...existing,
              nodes: [...existing.nodes, created],
            },
          },
          loading: false,
        };
      });

      // Background refetch to ensure server state is correct
      void get().refreshNodeContent(parentNodeId);

      return created;
    } catch (error) {
      console.error("Failed to create folder", error);
      set({ loading: false, error: "Failed to create folder" });
      return null;
    }
  },

  deleteFolder: async (nodeId, parentNodeId, skipTrash = false) => {
    if (get().loading) return false;

    set({ loading: true, error: null });

    try {
      await nodesApi.deleteNode(nodeId, skipTrash);

      // Optimistic update: remove folder from local cache
      if (parentNodeId) {
        set((prev) => {
          const existing = prev.contentByNodeId[parentNodeId];
          if (!existing) return { loading: false };

          return {
            contentByNodeId: {
              ...prev.contentByNodeId,
              [parentNodeId]: {
                ...existing,
                nodes: existing.nodes.filter((n) => n.id !== nodeId),
              },
            },
            loading: false,
          };
        });

        // Background refetch to ensure server state is correct
        void get().refreshNodeContent(parentNodeId);
      } else {
        set({ loading: false });
      }

      return true;
    } catch (error) {
      console.error("Failed to delete folder", error);
      set({ loading: false, error: "Failed to delete folder" });
      return false;
    }
  },

  renameFolder: async (nodeId, newName, parentNodeId) => {
    const trimmed = newName.trim();
    if (trimmed.length === 0) return false;
    if (get().loading) return false;

    const state = get();
    const currentContent = parentNodeId
      ? state.contentByNodeId[parentNodeId]
      : undefined;

    // Check for duplicate name (case-insensitive) in cached content
    if (currentContent) {
      const normalizedName = trimmed.toLowerCase();
      const duplicate = currentContent.nodes.find(
        (n) => n.id !== nodeId && n.name.toLowerCase() === normalizedName,
      );
      if (duplicate) {
        set({ error: "A folder with this name already exists" });
        return false;
      }
    }

    set({ loading: true, error: null });

    try {
      const updated = await nodesApi.renameNode(nodeId, { name: trimmed });

      // Optimistic update: rename folder in local cache
      if (parentNodeId) {
        set((prev) => {
          const existing = prev.contentByNodeId[parentNodeId];
          if (!existing) return { loading: false };

          return {
            contentByNodeId: {
              ...prev.contentByNodeId,
              [parentNodeId]: {
                ...existing,
                nodes: existing.nodes.map((n) =>
                  n.id === nodeId ? updated : n,
                ),
              },
            },
            loading: false,
          };
        });

        // Background refetch to ensure server state is correct
        void get().refreshNodeContent(parentNodeId);
      } else {
        set({ loading: false });
      }

      return true;
    } catch (error) {
      console.error("Failed to rename folder", error);
      set({ loading: false, error: "Failed to rename folder" });
      return false;
    }
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

  reset: () => {
    set({
      currentNode: null,
      ancestors: [],
      contentByNodeId: {},
      ancestorsByNodeId: {},
      rootNodeId: null,
      loading: false,
      error: null,
      lastUpdatedByNodeId: {},
    });
  },
      };
    },
    {
      name: NODES_STORAGE_KEY,
      partialize: (state) => ({
        currentNode: state.currentNode,
        ancestors: state.ancestors,
        rootNodeId: state.rootNodeId,
      }),
    },
  ),
);
