import { create } from "zustand";
import { nodesApi, type NodeContentDto } from "../api/nodesApi";
import { layoutsApi, type NodeDto } from "../api/layoutsApi";

type TrashState = {
  rootId: string | null;
  currentNode: NodeDto | null;
  ancestors: NodeDto[];
  contentByNodeId: Record<string, NodeContentDto | undefined>;

  loading: boolean;
  error: string | null;

  loadRoot: (options?: { force?: boolean; loadChildren?: boolean }) => Promise<NodeDto | null>;
  loadNode: (nodeId: string, options?: { loadChildren?: boolean }) => Promise<void>;
  refreshNodeContent: (nodeId: string) => Promise<void>;
  reset: () => void;
};

export const useTrashStore = create<TrashState>((set, get) => ({
  rootId: null,
  currentNode: null,
  ancestors: [],
  contentByNodeId: {},
  loading: false,
  error: null,

  loadRoot: async (options) => {
    const force = options?.force ?? false;
    const loadChildren = options?.loadChildren ?? true;
    const state = get();

    if (!force && state.rootId) {
      const hasCachedContent = !!state.contentByNodeId[state.rootId];
      if (!loadChildren || hasCachedContent) {
        await get().loadNode(state.rootId, { loadChildren });
        return get().currentNode;
      }
    }

    try {
      let rootId = state.rootId;
      if (!rootId || force) {
        const root = await layoutsApi.resolve({ nodeType: "trash" });
        rootId = root.id;
        set({ rootId });
      }
      await get().loadNode(rootId, { loadChildren });
      return get().currentNode;
    } catch (error) {
      console.error("Failed to resolve trash root", error);
      set({ loading: false, error: "Failed to resolve trash root" });
      return null;
    }
  },

  loadNode: async (nodeId, options) => {
    const state = get();
    const loadChildren = options?.loadChildren ?? true;
    if (state.loading && state.currentNode?.id === nodeId) return;

    const cachedContent = state.contentByNodeId[nodeId];
    const hasCachedData = !!cachedContent;
    const isRoot = state.rootId === nodeId;

    if (hasCachedData) {
      set({ loading: false, error: null });
    } else {
      set({ loading: true, error: null });
    }

    try {
      const [node, ancestors, content] = await Promise.all([
        nodesApi.getNode(nodeId),
        isRoot ? Promise.resolve([]) : nodesApi.getAncestors(nodeId, { nodeType: "trash" }),
        !loadChildren
          ? Promise.resolve(cachedContent)
          : hasCachedData
            ? Promise.resolve(cachedContent)
            : (await nodesApi.getChildren(nodeId, { nodeType: "trash" })).content,
      ]);

      set((prev) => ({
        currentNode: node,
        ancestors,
        contentByNodeId: loadChildren
          ? { ...prev.contentByNodeId, [nodeId]: content }
          : prev.contentByNodeId,
        loading: false,
        error: null,
      }));

      if (loadChildren && hasCachedData) {
        const freshContent = await nodesApi.getChildren(nodeId, { nodeType: "trash" });
        set((prev) => ({
          contentByNodeId: {
            ...prev.contentByNodeId,
            [nodeId]: freshContent.content,
          },
        }));
      }
    } catch (error) {
      console.error("Failed to load trash node", error);
      set({ loading: false, error: "Failed to load trash contents" });
    }
  },

  refreshNodeContent: async (nodeId) => {
    try {
      const content = await nodesApi.getChildren(nodeId, { nodeType: "trash" });
      set((prev) => ({
        contentByNodeId: {
          ...prev.contentByNodeId,
          [nodeId]: content.content,
        },
      }));
    } catch (error) {
      console.error("Failed to refresh trash content", error);
    }
  },

  reset: () => {
    set({
      rootId: null,
      currentNode: null,
      ancestors: [],
      contentByNodeId: {},
      loading: false,
      error: null,
    });
  },
}));
