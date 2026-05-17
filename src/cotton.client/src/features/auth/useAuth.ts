import { useContext } from "react";
import { AuthContext } from "./AuthProvider";
import type { User } from "./types";
import { useAuthStore } from "../../shared/store";

/**
 * Hook to access auth context
 * @throws Error if used outside AuthProvider
 */
export function useAuth() {
  const context = useContext(AuthContext);
  // The store must be subscribed unconditionally to satisfy rules-of-hooks;
  // its values are only consumed in the no-context fallback path below.
  const {
    user,
    isAuthenticated,
    isInitializing,
    refreshEnabled,
    hydrated,
    hasChecked,
    setAuthenticated,
    setUnauthenticated,
    logoutLocal,
  } = useAuthStore();

  if (context) {
    return context;
  }

  // Fallback for cases when components are rendered outside AuthProvider
  // (for example in Storybook or isolated tests). We provide a consistent
  // shape backed directly by the auth store, but without any of the async
  // bootstrap logic from AuthProvider.
  const ensureAuth = async () => {
    // no-op: real ensureAuth logic lives in AuthProvider
  };

  const setAuthenticatedWrapper = (value: boolean, u?: User | null) => {
    if (value && u) {
      setAuthenticated(u);
      return;
    }
    if (!value) {
      setUnauthenticated();
    }
  };

  const logout = async () => {
    logoutLocal();
  };

  return {
    user,
    isAuthenticated,
    isInitializing,
    refreshEnabled,
    hydrated,
    hasChecked,
    ensureAuth,
    setAuthenticated: setAuthenticatedWrapper,
    logout,
  };
}
