import { create } from "zustand";
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
  (set) => ({
    user: null,
    isAuthenticated: false,
    isInitializing: false,
    refreshEnabled: true,
    hydrated: true,
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

    logoutLocal: () =>
      set({
        user: null,
        isAuthenticated: false,
        refreshEnabled: false,
        hasChecked: true,
      }),
  }),
);

export const getRefreshEnabled = () => {
  const state = useAuthStore.getState();
  return state.hydrated && state.refreshEnabled;
};
