import { create } from "zustand";
import { nodesApi, type NodeContentDto } from "../api/nodesApi";
import { layoutsApi, type NodeDto } from "../api/layoutsApi";

type NodesState = {
  currentNode: NodeDto | null;
  ancestors: NodeDto[];
  contentByNodeId: Record<string, NodeContentDto | undefined>;

  loading: boolean;
  error: string | null;
  lastUpdatedByNodeId: Record<string, number | undefined>;

  loadRoot: (options?: { force?: boolean }) => Promise<NodeDto | null>;
  loadNode: (nodeId: string) => Promise<void>;
  refreshNodeContent: (nodeId: string) => Promise<void>;
  addFolderToCache: (parentNodeId: string, folder: NodeDto) => void;
  createFolder: (parentNodeId: string, name: string) => Promise<NodeDto | null>;
  deleteFolder: (nodeId: string, parentNodeId?: string) => Promise<boolean>;
  renameFolder: (nodeId: string, newName: string, parentNodeId?: string) => Promise<boolean>;
  reset: () => void;
};

export const useNodesStore = create<NodesState>((set, get) => ({
  currentNode: null,
  ancestors: [],
  contentByNodeId: {},
  loading: false,
  error: null,
  lastUpdatedByNodeId: {},

  loadRoot: async (options) => {
    const force = options?.force ?? false;
    const state = get();

    if (!force && state.currentNode && state.currentNode.parentId == null) {
      return state.currentNode;
    }
    try {
      const root = await layoutsApi.resolve();
      await get().loadNode(root.id);
      return root;
    } catch (error) {
      console.error("Failed to resolve root node", error);
      set({ loading: false, error: "Failed to resolve root node" });
      return null;
    }
  },

  loadNode: async (nodeId) => {
    const state = get();
    if (state.loading && state.currentNode?.id === nodeId) return;

    const cachedContent = state.contentByNodeId[nodeId];
    const hasCachedData = !!cachedContent;

    if (hasCachedData) {
      set({ loading: false, error: null });
    } else {
      set({ loading: true, error: null });
    }

    try {
      const [node, ancestors, content] = await Promise.all([
        nodesApi.getNode(nodeId),
        nodesApi.getAncestors(nodeId),
        hasCachedData ? Promise.resolve(cachedContent) : nodesApi.getChildren(nodeId),
      ]);

      set((prev) => ({
        currentNode: node,
        ancestors,
        contentByNodeId: {
          ...prev.contentByNodeId,
          [nodeId]: content,
        },
        lastUpdatedByNodeId: {
          ...prev.lastUpdatedByNodeId,
          [nodeId]: Date.now(),
        },
        loading: false,
        error: null,
      }));

      if (hasCachedData) {
        const freshContent = await nodesApi.getChildren(nodeId);
        set((prev) => ({
          contentByNodeId: {
            ...prev.contentByNodeId,
            [nodeId]: freshContent,
          },
          lastUpdatedByNodeId: {
            ...prev.lastUpdatedByNodeId,
            [nodeId]: Date.now(),
          },
        }));
      }
    } catch (error) {
      console.error("Failed to load node view", error);
      set({ loading: false, error: "Failed to load folder contents" });
    }
  },

  refreshNodeContent: async (nodeId) => {
    try {
      const content = await nodesApi.getChildren(nodeId);
      set((prev) => ({
        contentByNodeId: {
          ...prev.contentByNodeId,
          [nodeId]: content,
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
      const created = await nodesApi.createNode({ parentId: parentNodeId, name: trimmed });

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

  deleteFolder: async (nodeId, parentNodeId) => {
    if (get().loading) return false;

    set({ loading: true, error: null });

    try {
      await nodesApi.deleteNode(nodeId);

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
    const currentContent = parentNodeId ? state.contentByNodeId[parentNodeId] : undefined;

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
                nodes: existing.nodes.map((n) => n.id === nodeId ? updated : n),
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

  reset: () => {
    set({
      currentNode: null,
      ancestors: [],
      contentByNodeId: {},
      loading: false,
      error: null,
      lastUpdatedByNodeId: {},
    });
  },
}));
