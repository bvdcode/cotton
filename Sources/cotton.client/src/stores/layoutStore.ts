import { create } from "zustand";
import { getNodeChildren, resolvePath, type LayoutChildrenDto, type LayoutNodeDto } from "../api/layouts.ts";

interface LayoutState {
  currentNode: LayoutNodeDto | null;
  children: LayoutChildrenDto | null;
  parents: Record<string, string | null>; // nodeId -> parentId
  nodes: Record<string, LayoutNodeDto>; // cached node info by id (name, etc.)
  loading: boolean;
  error: string | null;
  resolveRoot: () => Promise<void>;
  loadChildren: (nodeId?: string) => Promise<void>;
  navigateToNode: (node: LayoutNodeDto) => Promise<void>;
  openNodeById: (nodeId: string) => Promise<void>;
}

export const useLayoutStore = create<LayoutState>((set, get) => ({
  currentNode: null,
  children: null,
  parents: {},
  nodes: {},
  loading: false,
  error: null,
  resolveRoot: async () => {
    set({ loading: true, error: null });
    try {
      const node = await resolvePath();
      set((s) => ({
        currentNode: node,
        loading: false,
        parents: { ...s.parents, [node.id]: node.parentId ?? null },
        nodes: { ...s.nodes, [node.id]: node },
      }));
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
      // update parents map and cache node info for all child nodes
      set((s) => ({
        children: data,
        loading: false,
        parents: {
          ...s.parents,
          ...Object.fromEntries((data.nodes ?? []).map((n) => [n.id, id])),
        },
        nodes: {
          ...s.nodes,
          ...Object.fromEntries((data.nodes ?? []).map((n) => [n.id, n])),
        },
      }));
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      set({ error: msg, loading: false });
    }
  },
  navigateToNode: async (node: LayoutNodeDto) => {
    set((s) => ({
      currentNode: node,
      parents: { ...s.parents, [node.id]: node.parentId ?? s.parents[node.id] ?? null },
      nodes: { ...s.nodes, [node.id]: node },
    }));
    await get().loadChildren(node.id);
  },
  openNodeById: async (nodeId: string) => {
    // Do not call missing endpoints; use known parent mapping if available
    const s = get();
    const pid = s.parents[nodeId] ?? null;
    const cached: LayoutNodeDto | undefined = s.nodes[nodeId];
    const node: LayoutNodeDto = cached
      ? { ...cached, parentId: cached.parentId ?? pid }
      : { id: nodeId, userLayoutId: "", parentId: pid, name: "", createdAt: "", updatedAt: "" } as LayoutNodeDto;
    set((prev) => ({ currentNode: node, nodes: { ...prev.nodes, [nodeId]: node } }));
    await get().loadChildren(nodeId);
  },
}));

export default useLayoutStore;
