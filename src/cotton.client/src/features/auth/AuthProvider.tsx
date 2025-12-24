import {
  useEffect,
  useCallback,
  createContext,
  type ReactNode,
} from "react";
import { authApi } from "../../shared/api/authApi";
import type { AuthContextValue, User } from "./types";
import { useAuthStore } from "../../shared/store";

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

  useEffect(() => {
    // Listen for logout event from httpClient interceptor
    const handleLogout = () => {
      setUnauthenticated();
    };
    window.addEventListener("auth:logout", handleLogout);

    return () => {
      window.removeEventListener("auth:logout", handleLogout);
    };
  }, []);

  const ensureAuth = useCallback(async () => {
    if (isAuthenticated || isInitializing) return;
    if (!hydrated) return;
    if (!refreshEnabled) return;

    setInitializing(true);
    try {
      const token = await authApi.refresh();
      if (token) {
        const userData = await authApi.me();
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
      setAuthenticatedInStore(u);
      return;
    }
    if (!value) {
      setUnauthenticated();
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
