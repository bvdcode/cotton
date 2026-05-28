import { create } from "zustand";
import {
  createJSONStorage,
  persist,
  type StateStorage,
} from "zustand/middleware";
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

const getLocalStorage = (): Storage | undefined => {
  if (typeof window === "undefined") {
    return undefined;
  }

  try {
    return window.localStorage ?? undefined;
  } catch {
    return undefined;
  }
};

const safeLocalStorage: StateStorage = {
  getItem: (key) => {
    try {
      return getLocalStorage()?.getItem(key) ?? null;
    } catch {
      return null;
    }
  },
  removeItem: (key) => {
    try {
      getLocalStorage()?.removeItem(key);
    } catch {
      // best-effort: auth state should still update when storage is blocked
    }
  },
  setItem: (key, value) => {
    try {
      getLocalStorage()?.setItem(key, value);
    } catch {
      // best-effort: auth state should still update when storage is blocked
    }
  },
};

export const useAuthStore = create<AuthStoreState>()(
  persist(
    (set) => ({
      user: null,
      isAuthenticated: false,
      isInitializing: false,
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
      storage: createJSONStorage(() => safeLocalStorage),
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
