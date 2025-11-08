import { create } from "zustand";
import api from "../api/http.ts";
import { API_ENDPOINTS } from "../config.ts";
import { persist } from "zustand/middleware";

type Role = "User" | "Admin";

export interface AuthUser {
  id: string;
  name: string;
  role: Role;
}

interface AuthState {
  isAuthenticated: boolean;
  user: AuthUser | null;
  token: string | null;
  loginLocal: (user?: Partial<AuthUser>) => void;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
}

export const useAuth = create<AuthState>()(
  persist(
    (set) => ({
      // Defaults: not authenticated until token obtained
      isAuthenticated: false,
      user: null,
      token: null,
      loginLocal: (user) =>
        set({
          isAuthenticated: true,
          user: {
            id: user?.id ?? "1",
            name: user?.name ?? "Mock User",
            role: (user?.role as Role) ?? "Admin",
          },
        }),
      login: async (username: string, password: string) => {
        // Login endpoint with username and password
        const res = await api.post<{ token: string; user: AuthUser }>(
          `${API_ENDPOINTS.auth}/login`,
          { username, password },
        );
        const { token, user } = res.data;
        set({ token, user, isAuthenticated: true });
      },
      logout: () => set({ isAuthenticated: false, user: null, token: null }),
    }),
    {
      name: "auth-storage",
    },
  ),
);

export default useAuth;
