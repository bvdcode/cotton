import { create } from "zustand";
import { settingsApi } from "../api/settingsApi";
import { isAxiosError } from "../api/httpClient";

interface SetupStatusState {
  isInitialized: boolean | null;
  loading: boolean;
  loaded: boolean;
  error: string | null;
  fetchSetupStatus: (options?: { force?: boolean }) => Promise<void>;
  reset: () => void;
}

export const useSetupStatusStore = create<SetupStatusState>((set, get) => ({
  isInitialized: null,
  loading: false,
  loaded: false,
  error: null,

  fetchSetupStatus: async (options) => {
    const force = options?.force ?? false;
    const state = get();

    if (state.loading) return;
    if (state.loaded && !force) return;

    set({ loading: true, error: null });

    try {
      const isInitialized = await settingsApi.getIsSetupComplete();
      set({
        isInitialized,
        loading: false,
        loaded: true,
        error: null,
      });
    } catch (error) {
      if (isAxiosError(error) && error.response?.status === 403) {
        // Non-admin users cannot access this endpoint.
        set({
          isInitialized: true,
          loading: false,
          loaded: true,
          error: null,
        });
        return;
      }

      console.error("Failed to load setup status", error);
      set({
        isInitialized: true,
        loading: false,
        loaded: true,
        error: "Failed to load setup status",
      });
    }
  },

  reset: () => {
    set({
      isInitialized: null,
      loading: false,
      loaded: false,
      error: null,
    });
  },
}));
