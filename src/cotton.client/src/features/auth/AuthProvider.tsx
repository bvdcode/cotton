import {
  useEffect,
  useCallback,
  createContext,
  type ReactNode,
} from "react";
import { authApi } from "../../shared/api/authApi";
import type { AuthContextValue, User } from "./types";
import { useAuthStore } from "../../shared/store";
import { useUserPreferencesStore } from "../../shared/store/userPreferencesStore";
import { resetUserScopedStores } from "../../shared/store/resetUserScopedStores";

const AuthContext = createContext<AuthContextValue | null>(null);

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const user = useAuthStore((s) => s.user);
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const isInitializing = useAuthStore((s) => s.isInitializing);
  const refreshEnabled = useAuthStore((s) => s.refreshEnabled);
  const hydrated = useAuthStore((s) => s.hydrated);
  const hasChecked = useAuthStore((s) => s.hasChecked);
  const setInitializing = useAuthStore((s) => s.setInitializing);
  const setAuthenticatedInStore = useAuthStore((s) => s.setAuthenticated);
  const setUnauthenticated = useAuthStore((s) => s.setUnauthenticated);
  const logoutLocal = useAuthStore((s) => s.logoutLocal);
  const setHasChecked = useAuthStore((s) => s.setHasChecked);

  const userId = user?.id ?? null;

  useEffect(() => {
    // Listen for logout event from httpClient interceptor
    const handleLogout = () => {
      logoutLocal();
      resetUserScopedStores(null);
    };
    window.addEventListener("auth:logout", handleLogout);

    return () => {
      window.removeEventListener("auth:logout", handleLogout);
    };
  }, [logoutLocal]);

  useEffect(() => {
    // Security: prevent cross-user cached data reuse.
    // When auth identity changes, clear all user-scoped caches.
    // During initial auth bootstrap user can be temporarily null,
    // so defer reset until the first auth check is completed.
    if (!hydrated) return;
    if (!hasChecked && refreshEnabled) return;

    resetUserScopedStores(userId);
  }, [userId, hydrated, hasChecked, refreshEnabled]);

  useEffect(() => {
    if (!user) {
      useUserPreferencesStore.getState().reset();
      return;
    }
    useUserPreferencesStore.getState().hydrateFromUser(user);
  }, [user]);

  const ensureAuth = useCallback(async () => {
    if (isAuthenticated || isInitializing) return;
    if (!hydrated) return;
    if (!refreshEnabled) {
      setHasChecked(true);
      return;
    }

    setInitializing(true);
    try {
      const token = await authApi.refresh();
      if (token) {
        const userData = await authApi.me();

        // Ensure stale persisted data from another identity is cleared
        // before protected routes can render for this user.
        resetUserScopedStores(userData.id);
        setAuthenticatedInStore(userData);
      } else {
        setUnauthenticated();
      }
    } catch (error) {
      console.error("Failed to fetch user data:", error);
      setUnauthenticated();
    } finally {
      setHasChecked(true);
      setInitializing(false);
    }
  }, [isAuthenticated, isInitializing, hydrated, refreshEnabled, setInitializing, setAuthenticatedInStore, setUnauthenticated, setHasChecked]);

  const setAuthenticated = useCallback((value: boolean, u?: User | null) => {
    if (value && u) {
      const authState = useAuthStore.getState();
      const currentUserId = authState.user?.id ?? null;
      const shouldResetUserScopedStores = !authState.isAuthenticated || currentUserId !== u.id;

      // Keep user-scoped caches when only profile fields are updated for the same identity.
      if (shouldResetUserScopedStores) {
        resetUserScopedStores(u.id);
      }

      setAuthenticatedInStore(u);
      return;
    }
    if (!value) {
      setUnauthenticated();
      resetUserScopedStores(null);
    }
  }, [setAuthenticatedInStore, setUnauthenticated]);

  const logout = useCallback(async () => {
    try {
      await authApi.logout();
    } catch (error) {
      // Ignore logout errors - still clear local state
      console.error("Logout error:", error);
    }
    logoutLocal();
    resetUserScopedStores(null);
  }, [logoutLocal]);

  const value: AuthContextValue = {
    user,
    isAuthenticated,
    isInitializing,
    refreshEnabled,
    hydrated,
    hasChecked,
    ensureAuth,
    setAuthenticated,
    logout,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export { AuthContext };
