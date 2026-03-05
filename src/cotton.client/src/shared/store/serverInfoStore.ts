import { create } from "zustand";
import { settingsApi, type PublicServerInfo } from "../api/settingsApi";

interface ServerInfoState {
  data: PublicServerInfo | null;
  loading: boolean;
  loaded: boolean;
  error: string | null;
  fetchServerInfo: (options?: { force?: boolean }) => Promise<void>;
  reset: () => void;
}

export const useServerInfoStore = create<ServerInfoState>((set, get) => ({
  data: null,
  loading: false,
  loaded: false,
  error: null,

  fetchServerInfo: async (options) => {
    const force = options?.force ?? false;
    const state = get();

    if (state.loading) return;
    if (state.loaded && !force) return;

    set({ loading: true, error: null });

    try {
      const data = await settingsApi.getPublicInfo();
      set({
        data,
        loading: false,
        loaded: true,
        error: null,
      });
    } catch (error) {
      console.error("Failed to load server info", error);
      set({ loading: false, error: "Failed to load server info" });
    }
  },

  reset: () => {
    set({
      data: null,
      loading: false,
      loaded: false,
      error: null,
    });
  },
}));
