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
  createFolder: (parentNodeId: string, name: string) => Promise<NodeDto | null>;
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

    set({ loading: true, error: null });

    try {
      const [node, ancestors, content] = await Promise.all([
        nodesApi.getNode(nodeId),
        nodesApi.getAncestors(nodeId),
        nodesApi.getChildren(nodeId),
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
    } catch (error) {
      console.error("Failed to load node view", error);
      set({ loading: false, error: "Failed to load folder contents" });
    }
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
      void get().loadNode(parentNodeId);

      return created;
    } catch (error) {
      console.error("Failed to create folder", error);
      set({ loading: false, error: "Failed to create folder" });
      return null;
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
