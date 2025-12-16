import {
  useState,
  useEffect,
  useCallback,
  createContext,
  type ReactNode,
} from "react";
import { authApi } from "../../shared/api/authApi";
import type { AuthContextValue, User } from "./types";

const AuthContext = createContext<AuthContextValue | null>(null);

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [user, setUser] = useState<User | null>(null);
  const [isInitializing, setIsInitializing] = useState(true);
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(false);

  useEffect(() => {
    let mounted = true;
    (async () => {
      console.log("[AuthProvider] Starting initialization...");
      // Attempt silent refresh before finishing initialization
      const token = await authApi.refresh();
      console.log("[AuthProvider] Refresh result:", token ? "token received" : "no token");
      if (!mounted) return;

      // If token received, validate it by fetching user data
      if (token) {
        try {
          const userData = await authApi.me();
          console.log("[AuthProvider] User data fetched:", userData);
          if (mounted) {
            setUser(userData);
            setIsAuthenticated(true);
          }
        } catch (error) {
          // Token invalid or /me failed
          console.error("[AuthProvider] Failed to fetch user data:", error);
          if (mounted) {
            setIsAuthenticated(false);
            setUser(null);
          }
        }
      }

      if (mounted) {
        console.log("[AuthProvider] Initialization complete. isAuthenticated:", !!token);
        setIsInitializing(false);
      }
    })();

    // Listen for logout event from httpClient interceptor
    const handleLogout = () => {
      setIsAuthenticated(false);
      setUser(null);
    };
    window.addEventListener("auth:logout", handleLogout);

    return () => {
      mounted = false;
      window.removeEventListener("auth:logout", handleLogout);
    };
  }, []);

  const setAuthenticated = useCallback((value: boolean, u?: User | null) => {
    setIsAuthenticated(value);
    if (typeof u !== "undefined") {
      setUser(u ?? null);
    }
  }, []);

  const logout = useCallback(async () => {
    try {
      await authApi.logout();
    } catch (error) {
      // Ignore logout errors - still clear local state
      console.error("Logout error:", error);
    }
    setIsAuthenticated(false);
    setUser(null);
  }, []);

  const value: AuthContextValue = {
    user,
    isAuthenticated,
    isInitializing,
    setAuthenticated,
    logout,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export { AuthContext };
