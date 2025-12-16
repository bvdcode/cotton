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
  const [isInitializing, setIsInitializing] = useState(false);
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(false);

  useEffect(() => {
    // Listen for logout event from httpClient interceptor
    const handleLogout = () => {
      setIsAuthenticated(false);
      setUser(null);
    };
    window.addEventListener("auth:logout", handleLogout);

    return () => {
      window.removeEventListener("auth:logout", handleLogout);
    };
  }, []);

  const ensureAuth = useCallback(async () => {
    if (isAuthenticated || isInitializing) return;

    setIsInitializing(true);
    try {
      const token = await authApi.refresh();
      if (token) {
        const userData = await authApi.me();
        setUser(userData);
        setIsAuthenticated(true);
      } else {
        setIsAuthenticated(false);
        setUser(null);
      }
    } catch (error) {
      console.error("Failed to fetch user data:", error);
      setIsAuthenticated(false);
      setUser(null);
    } finally {
      setIsInitializing(false);
    }
  }, [isAuthenticated, isInitializing]);

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
    ensureAuth,
    setAuthenticated,
    logout,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export { AuthContext };
