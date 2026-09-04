import { create } from "zustand";
import {
  createJSONStorage,
  persist,
  type StateStorage,
} from "zustand/middleware";
import { AUTH_STORAGE_KEY } from "../config/storageKeys";
import type { AuthPhase, User } from "../../features/auth/types";

type AuthStoreState = {
  user: User | null;
  phase: AuthPhase;
  refreshEnabled: boolean;
  setAuthenticated: (user: User) => void;
  setBooting: () => void;
  setUnauthenticated: () => void;
  setUnavailable: () => void;
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
      phase: "booting",
      refreshEnabled: true,

      setAuthenticated: (user) =>
        set({
          user,
          phase: "authenticated",
          refreshEnabled: true,
        }),

      setBooting: () =>
        set({
          phase: "booting",
        }),

      setUnauthenticated: () =>
        set({
          user: null,
          phase: "anonymous",
        }),

      setUnavailable: () =>
        set({
          phase: "unavailable",
        }),

      logoutLocal: () =>
        set({
          user: null,
          phase: "anonymous",
          refreshEnabled: false,
        }),
    }),
    {
      name: AUTH_STORAGE_KEY,
      storage: createJSONStorage(() => safeLocalStorage),
      partialize: (state) => ({ refreshEnabled: state.refreshEnabled }),
    },
  ),
);

export const getRefreshEnabled = () => {
  const state = useAuthStore.getState();
  return useAuthStore.persist.hasHydrated() && state.refreshEnabled;
};

export const waitForAuthStoreHydration = async (): Promise<void> => {
  if (useAuthStore.persist.hasHydrated()) {
    return;
  }

  await new Promise<void>((resolve) => {
    let settled = false;
    let unsubscribe: () => void = () => undefined;
    const finish = () => {
      if (settled) return;
      settled = true;
      unsubscribe();
      resolve();
    };

    unsubscribe = useAuthStore.persist.onFinishHydration(finish);
    if (useAuthStore.persist.hasHydrated()) {
      finish();
    }
  });
};
