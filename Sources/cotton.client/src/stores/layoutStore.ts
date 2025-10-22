import { create } from "zustand";
import { getNodeChildren, resolvePath, type LayoutChildrenDto, type LayoutNodeDto } from "../api/layouts.ts";

interface LayoutState {
  currentNode: LayoutNodeDto | null;
  children: LayoutChildrenDto | null;
  loading: boolean;
  error: string | null;
  resolveRoot: () => Promise<void>;
  loadChildren: (nodeId?: string) => Promise<void>;
  navigateToNode: (node: LayoutNodeDto) => Promise<void>;
}

export const useLayoutStore = create<LayoutState>((set, get) => ({
  currentNode: null,
  children: null,
  loading: false,
  error: null,
  resolveRoot: async () => {
    set({ loading: true, error: null });
    try {
      const node = await resolvePath();
      set({ currentNode: node, loading: false });
      await get().loadChildren(node.id);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      set({ error: msg, loading: false });
    }
  },
  loadChildren: async (nodeId?: string) => {
    const id = nodeId ?? get().currentNode?.id;
    if (!id) return;
    set({ loading: true, error: null });
    try {
      const data = await getNodeChildren(id);
      set({ children: data, loading: false });
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      set({ error: msg, loading: false });
    }
  },
  navigateToNode: async (node: LayoutNodeDto) => {
    set({ currentNode: node });
    await get().loadChildren(node.id);
  },
}));

export default useLayoutStore;
