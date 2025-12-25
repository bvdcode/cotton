import { create } from "zustand";
import { settingsApi, type ServerSettings } from "../api/settingsApi";

interface SettingsState {
  data: ServerSettings | null;
  loading: boolean;
  loaded: boolean;
  error: string | null;
  lastUpdated: number | null;
  fetchSettings: (options?: { force?: boolean }) => Promise<void>;
  reset: () => void;
}

export const useSettingsStore = create<SettingsState>((set, get) => ({
  data: null,
  loading: false,
  loaded: false,
  error: null,
  lastUpdated: null,

  fetchSettings: async (options) => {
    const force = options?.force ?? false;
    const state = get();

    if (state.loading) return;
    if (state.loaded && !force) return;

    set({ loading: true, error: null });

    try {
      const data = await settingsApi.get();
      set({
        data,
        loading: false,
        loaded: true,
        lastUpdated: Date.now(),
        error: null,
      });
    } catch (error) {
      console.error("Failed to load settings", error);
      set({ loading: false, error: "Failed to load settings" });
    }
  },

  reset: () => {
    set({
      data: null,
      loading: false,
      loaded: false,
      error: null,
      lastUpdated: null,
    });
  },
}));
