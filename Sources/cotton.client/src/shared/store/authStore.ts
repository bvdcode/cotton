import { create } from "zustand";
import { persist } from "zustand/middleware";
import { AUTH_STORAGE_KEY } from "../config/storageKeys";
import type { User } from "../../features/auth/types";

type AuthStoreState = {
  user: User | null;
  isAuthenticated: boolean;
  isInitializing: boolean;
  refreshEnabled: boolean;
  hydrated: boolean;
  hasChecked: boolean;
  setInitializing: (value: boolean) => void;
  setHydrated: (value: boolean) => void;
  setHasChecked: (value: boolean) => void;
  setAuthenticated: (user: User) => void;
  setUnauthenticated: () => void;
  logoutLocal: () => void;
};

export const useAuthStore = create<AuthStoreState>()(
  persist(
    (set) => ({
      user: null,
      isAuthenticated: false,
      isInitializing: false,
      // Allow trying to restore session by default.
      // Explicit logout will persist `refreshEnabled=false` to block refresh attempts.
      refreshEnabled: true,
      hydrated: false,
      hasChecked: false,

      setInitializing: (value) => set({ isInitializing: value }),
      setHydrated: (value) => set({ hydrated: value }),
      setHasChecked: (value) => set({ hasChecked: value }),

      setAuthenticated: (user) =>
        set({
          user,
          isAuthenticated: true,
          refreshEnabled: true,
          hasChecked: true,
        }),

      setUnauthenticated: () =>
        set({
          user: null,
          isAuthenticated: false,
          hasChecked: true,
        }),

      // Explicit user logout: also disables refresh until next login.
      logoutLocal: () =>
        set({
          user: null,
          isAuthenticated: false,
          refreshEnabled: false,
          hasChecked: true,
        }),
    }),
    {
      name: AUTH_STORAGE_KEY,
      partialize: (state) => ({ refreshEnabled: state.refreshEnabled }),
      onRehydrateStorage: () => (state) => {
        state?.setHydrated(true);
      },
    },
  ),
);

export const getRefreshEnabled = () => {
  const state = useAuthStore.getState();
  return state.hydrated && state.refreshEnabled;
};
