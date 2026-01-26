import { create } from "zustand";
import { nodesApi, type NodeContentDto } from "../api/nodesApi";
import { layoutsApi, type NodeDto } from "../api/layoutsApi";

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
  loadNode: (nodeId: string, options?: { loadChildren?: boolean }) => Promise<void>;
  refreshNodeContent: (nodeId: string) => Promise<void>;
  addFolderToCache: (parentNodeId: string, folder: NodeDto) => void;
  createFolder: (parentNodeId: string, name: string) => Promise<NodeDto | null>;
  deleteFolder: (nodeId: string, parentNodeId?: string, skipTrash?: boolean) => Promise<boolean>;
  renameFolder: (
    nodeId: string,
    newName: string,
    parentNodeId?: string,
  ) => Promise<boolean>;
  reset: () => void;
};

export const useNodesStore = create<NodesState>((set, get) => ({
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
    if (state.loading && state.currentNode?.id === nodeId) return;

    if (!loadChildren) {
      set({ loading: true, error: null });

      try {
        let node: NodeDto | null = null;
        if (state.currentNode) {
          const parentContent = state.contentByNodeId[state.currentNode.id];
          node = parentContent?.nodes.find(n => n.id === nodeId) ?? null;
        }
        if (!node) {
          node = await nodesApi.getNode(nodeId);
        }
        
        let ancestors = state.ancestorsByNodeId[nodeId];
        if (!ancestors) {
          if (state.currentNode && node.parentId === state.currentNode.id) {
            ancestors = [...state.ancestors, state.currentNode];
          } else {
            ancestors = await nodesApi.getAncestors(nodeId);
          }
        }

        set((prev) => ({
          currentNode: node!,
          ancestors,
          ancestorsByNodeId: {
            ...prev.ancestorsByNodeId,
            [nodeId]: ancestors,
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

      return;
    }

    set({ loading: true, error: null });

    try {
      let node: NodeDto | null = null;
      if (state.currentNode) {
        const parentContent = state.contentByNodeId[state.currentNode.id];
        node = parentContent?.nodes.find(n => n.id === nodeId) ?? null;
      }
      if (!node) {
        node = await nodesApi.getNode(nodeId);
      }
      
      let ancestors = state.ancestorsByNodeId[nodeId];
      if (!ancestors) {
        if (state.currentNode && node.parentId === state.currentNode.id) {
          ancestors = [...state.ancestors, state.currentNode];
        } else {
          ancestors = await nodesApi.getAncestors(nodeId);
        }
      }
      
      const content = (await nodesApi.getChildren(nodeId)).content;

      set((prev) => ({
        currentNode: node!,
        ancestors,
        ancestorsByNodeId: {
          ...prev.ancestorsByNodeId,
          [nodeId]: ancestors,
        },
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
}));
