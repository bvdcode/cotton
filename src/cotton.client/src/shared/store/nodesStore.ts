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

    set({ loading: true, error: null });

    try {
      const created = await nodesApi.createNode({ parentId: parentNodeId, name: trimmed });
      // Refetch current folder to include the new node.
      await get().loadNode(parentNodeId);
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
