import { create } from "zustand";
import { persist } from "zustand/middleware";
import { AUTH_STORAGE_KEY } from "../config/storageKeys";
import type { User } from "../../features/auth/types";

type AuthStoreState = {
  user: User | null;
  isAuthenticated: boolean;
  isInitializing: boolean;
  refreshEnabled: boolean;
  setInitializing: (value: boolean) => void;
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
      refreshEnabled: false,

      setInitializing: (value) => set({ isInitializing: value }),

      setAuthenticated: (user) =>
        set({
          user,
          isAuthenticated: true,
          refreshEnabled: true,
        }),

      setUnauthenticated: () =>
        set({
          user: null,
          isAuthenticated: false,
        }),

      // Explicit user logout: also disables refresh until next login.
      logoutLocal: () =>
        set({
          user: null,
          isAuthenticated: false,
          refreshEnabled: false,
        }),
    }),
    {
      name: AUTH_STORAGE_KEY,
      partialize: (state) => ({ refreshEnabled: state.refreshEnabled }),
    },
  ),
);

export const getRefreshEnabled = () => useAuthStore.getState().refreshEnabled;
