import { create } from "zustand";
import { persist } from "zustand/middleware";
import { layoutsApi, type LayoutStatsDto, type NodeDto } from "../api/layoutsApi";
import { LAYOUTS_STORAGE_KEY } from "../config/storageKeys";

type LayoutsState = {
  rootNode: NodeDto | null;
  statsByLayoutId: Record<string, LayoutStatsDto | undefined>;
  loadingRoot: boolean;
  loadingStats: boolean;
  error: string | null;
  lastUpdatedRoot: number | null;
  lastUpdatedStatsByLayoutId: Record<string, number | undefined>;

  resolveRootNode: (options?: { force?: boolean }) => Promise<NodeDto | null>;
  fetchLayoutStats: (
    layoutId: string,
    options?: { force?: boolean },
  ) => Promise<LayoutStatsDto | null>;

  /**
   * Home page initializer:
   * - Uses cached data immediately if present.
   * - Always triggers a refetch to keep data correct after reload.
   */
  ensureHomeData: () => Promise<void>;

  reset: () => void;
};

export const useLayoutsStore = create<LayoutsState>()(
  persist(
    (set, get) => ({
      rootNode: null,
      statsByLayoutId: {},
      loadingRoot: false,
      loadingStats: false,
      error: null,
      lastUpdatedRoot: null,
      lastUpdatedStatsByLayoutId: {},

      resolveRootNode: async (options) => {
        const force = options?.force ?? false;
        const state = get();

        if (state.loadingRoot) return state.rootNode;
        if (state.rootNode && !force) return state.rootNode;

        set({ loadingRoot: true, error: null });

        try {
          const rootNode = await layoutsApi.resolve();
          set({
            rootNode,
            loadingRoot: false,
            error: null,
            lastUpdatedRoot: Date.now(),
          });
          return rootNode;
        } catch (error) {
          console.error("Failed to resolve root layout", error);
          set({ loadingRoot: false, error: "Failed to resolve root layout" });
          return null;
        }
      },

      fetchLayoutStats: async (layoutId, options) => {
        const force = options?.force ?? false;
        const state = get();

        if (state.loadingStats) return state.statsByLayoutId[layoutId] ?? null;
        if (state.statsByLayoutId[layoutId] && !force) {
          return state.statsByLayoutId[layoutId] ?? null;
        }

        set({ loadingStats: true, error: null });

        try {
          const stats = await layoutsApi.getStats(layoutId);
          set((prev) => ({
            statsByLayoutId: {
              ...prev.statsByLayoutId,
              [layoutId]: stats,
            },
            loadingStats: false,
            error: null,
            lastUpdatedStatsByLayoutId: {
              ...prev.lastUpdatedStatsByLayoutId,
              [layoutId]: Date.now(),
            },
          }));
          return stats;
        } catch (error) {
          console.error("Failed to load layout stats", error);
          set({ loadingStats: false, error: "Failed to load layout stats" });
          return null;
        }
      },

      ensureHomeData: async () => {
        // Use cached data immediately if present. Refetch in background so UI doesn't
        // block/flicker when user already has data from persisted store.
        const root = await get().resolveRootNode({ force: false });
        const cachedLayoutId = root?.layoutId;

        if (cachedLayoutId) {
          await get().fetchLayoutStats(cachedLayoutId, { force: false });
        }

        void (async () => {
          const refreshedRoot = await get().resolveRootNode({ force: true });
          const refreshedLayoutId = refreshedRoot?.layoutId ?? cachedLayoutId;
          if (!refreshedLayoutId) return;

          await get().fetchLayoutStats(refreshedLayoutId, { force: true });
        })();
      },

      reset: () => {
        set({
          rootNode: null,
          statsByLayoutId: {},
          loadingRoot: false,
          loadingStats: false,
          error: null,
          lastUpdatedRoot: null,
          lastUpdatedStatsByLayoutId: {},
        });
      },
    }),
    {
      name: LAYOUTS_STORAGE_KEY,
      partialize: (state) => ({
        rootNode: state.rootNode,
        statsByLayoutId: state.statsByLayoutId,
        lastUpdatedRoot: state.lastUpdatedRoot,
        lastUpdatedStatsByLayoutId: state.lastUpdatedStatsByLayoutId,
      }),
    },
  ),
);
