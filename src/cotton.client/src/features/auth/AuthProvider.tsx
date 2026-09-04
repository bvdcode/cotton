import { useEffect, useCallback, useRef, type ReactNode } from "react";
import { authApi } from "../../shared/api/authApi";
import type { AuthContextValue, RestoreResult, User } from "./types";
import { useAuthStore } from "../../shared/store";
import { waitForAuthStoreHydration } from "../../shared/store/authStore";
import { useUserPreferencesStore } from "../../shared/store/userPreferencesStore";
import { resetUserScopedStores } from "../../shared/store/resetUserScopedStores";
import { JUST_UNLOCKED_STORAGE_KEY } from "./authStorageKeys";
import { consumeOidcSignInPending } from "./oidcSignInSession";
import { AuthContext } from "./AuthContext";

const AUTH_RETRY_AFTER_UNLOCK_TIMEOUT_MS = 10000;
const AUTH_RETRY_AFTER_UNLOCK_INTERVAL_MS = 350;

const delay = (milliseconds: number): Promise<void> =>
  new Promise((resolve) => setTimeout(resolve, milliseconds));

const consumeJustUnlockedMarker = (): boolean => {
  if (typeof window === "undefined") {
    return false;
  }

  try {
    const value = window.sessionStorage.getItem(JUST_UNLOCKED_STORAGE_KEY);
    window.sessionStorage.removeItem(JUST_UNLOCKED_STORAGE_KEY);
    return value !== null;
  } catch {
    return false;
  }
};

interface RestoreAuthSessionOptions {
  allowWhenRefreshDisabled?: boolean;
}

const restoreAuthSession = async (
  options: RestoreAuthSessionOptions = {},
): Promise<RestoreResult> => {
  return await authApi.restoreSession({
    allowWhenRefreshDisabled: options.allowWhenRefreshDisabled,
  });
};

const waitForAuthSessionAfterUnlock = async (
  options: RestoreAuthSessionOptions = {},
): Promise<RestoreResult> => {
  const deadline = Date.now() + AUTH_RETRY_AFTER_UNLOCK_TIMEOUT_MS;

  while (Date.now() < deadline) {
    try {
      return await restoreAuthSession(options);
    } catch {
      // Unlock completion and auth availability are not atomic.
    }

    if (
      !options.allowWhenRefreshDisabled &&
      !useAuthStore.getState().refreshEnabled
    ) {
      return { kind: "anonymous" };
    }

    await delay(AUTH_RETRY_AFTER_UNLOCK_INTERVAL_MS);
  }

  return await restoreAuthSession(options);
};

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const user = useAuthStore((s) => s.user);
  const phase = useAuthStore((s) => s.phase);
  const refreshEnabled = useAuthStore((s) => s.refreshEnabled);
  const setAuthenticatedInStore = useAuthStore((s) => s.setAuthenticated);
  const setBooting = useAuthStore((s) => s.setBooting);
  const setUnauthenticated = useAuthStore((s) => s.setUnauthenticated);
  const setUnavailable = useAuthStore((s) => s.setUnavailable);
  const logoutLocal = useAuthStore((s) => s.logoutLocal);
  const restorePromiseRef = useRef<Promise<void> | null>(null);
  const lastResetUserIdRef = useRef<string | null | undefined>(undefined);

  const userId = user?.id ?? null;
  const isAuthenticated = phase === "authenticated";
  const resetForIdentity = useCallback((nextUserId: string | null): void => {
    resetUserScopedStores(nextUserId);
    lastResetUserIdRef.current = nextUserId;
  }, []);

  useEffect(() => {
    // Listen for logout event from httpClient interceptor
    const handleLogout = () => {
      logoutLocal();
      resetForIdentity(null);
    };
    window.addEventListener("auth:logout", handleLogout);

    return () => {
      window.removeEventListener("auth:logout", handleLogout);
    };
  }, [logoutLocal, resetForIdentity]);

  useEffect(() => {
    // Security: prevent cross-user cached data reuse.
    // When auth identity changes, clear all user-scoped caches.
    if (phase === "booting") return;
    if (lastResetUserIdRef.current === userId) return;

    resetForIdentity(userId);
  }, [phase, resetForIdentity, userId]);

  useEffect(() => {
    if (!user) {
      useUserPreferencesStore.getState().reset();
      return;
    }
    useUserPreferencesStore.getState().hydrateFromUser(user);
  }, [user]);

  const restoreSession = useCallback((): Promise<void> => {
    if (restorePromiseRef.current) {
      return restorePromiseRef.current;
    }

    const promise = (async () => {
      await waitForAuthStoreHydration();
      const authState = useAuthStore.getState();
      if (authState.phase === "authenticated") {
        return;
      }

      const hasPendingOidcSignIn = consumeOidcSignInPending();
      if (!authState.refreshEnabled && !hasPendingOidcSignIn) {
        authState.setUnauthenticated();
        return;
      }

      setBooting();

      const restoreOptions: RestoreAuthSessionOptions = {
        allowWhenRefreshDisabled: hasPendingOidcSignIn,
      };
      const shouldRetryAfterUnlock = consumeJustUnlockedMarker();
      const result = shouldRetryAfterUnlock
        ? await waitForAuthSessionAfterUnlock(restoreOptions)
        : await restoreAuthSession(restoreOptions);

      switch (result.kind) {
        case "authenticated":
          resetForIdentity(result.user.id);
          useAuthStore.getState().setAuthenticated(result.user);
          return;
        case "anonymous":
          useAuthStore.getState().setUnauthenticated();
          return;
      }
    })().catch(() => {
      setUnavailable();
    });

    restorePromiseRef.current = promise;
    const clearPromise = (): void => {
      if (restorePromiseRef.current === promise) {
        restorePromiseRef.current = null;
      }
    };
    void promise.then(clearPromise, clearPromise);
    return promise;
  }, [resetForIdentity, setBooting, setUnavailable]);

  const setAuthenticated = useCallback(
    (value: boolean, u?: User | null) => {
      if (value && u) {
        const authState = useAuthStore.getState();
        const currentUserId = authState.user?.id ?? null;
        const shouldResetUserScopedStores =
          authState.phase !== "authenticated" || currentUserId !== u.id;

        // Keep user-scoped caches when only profile fields are updated for the same identity.
        if (shouldResetUserScopedStores) {
          resetForIdentity(u.id);
        }

        setAuthenticatedInStore(u);
        return;
      }
      if (!value) {
        setUnauthenticated();
        resetForIdentity(null);
      }
    },
    [resetForIdentity, setAuthenticatedInStore, setUnauthenticated],
  );

  const logout = useCallback(async () => {
    logoutLocal();
    resetForIdentity(null);
    try {
      await authApi.logout();
    } catch (error) {
      // Ignore logout errors - still clear local state
      console.error("Logout error:", error);
    }
  }, [logoutLocal, resetForIdentity]);

  const value: AuthContextValue = {
    user,
    phase,
    isAuthenticated,
    refreshEnabled,
    restoreSession,
    setAuthenticated,
    logout,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
