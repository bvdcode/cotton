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
  accessToken: string | null;
  refreshToken: string | null;
  loginLocal: (user?: Partial<AuthUser>) => void;
  login: (username: string, password: string) => Promise<void>;
  refresh: () => Promise<void>;
  logout: () => void;
}

export const useAuth = create<AuthState>()(
  persist(
    (set, get) => ({
      // Defaults: not authenticated until token obtained
      isAuthenticated: false,
      user: null,
      accessToken: null,
      refreshToken: null,
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
        const res = await api.post<{
          accessToken: string;
          refreshToken: string;
          user: AuthUser;
        }>(`${API_ENDPOINTS.auth}/login`, { username, password });
        const { accessToken, refreshToken, user } = res.data;
        set({
          accessToken,
          refreshToken,
          user,
          isAuthenticated: true,
        });
      },
      refresh: async () => {
        const { refreshToken } = get();
        if (!refreshToken) {
          throw new Error("No refresh token available");
        }
        const res = await api.post<{
          accessToken: string;
          refreshToken: string;
        }>(`${API_ENDPOINTS.auth}/refresh`, { refreshToken });
        const { accessToken: newAccessToken, refreshToken: newRefreshToken } =
          res.data;
        set({
          accessToken: newAccessToken,
          refreshToken: newRefreshToken,
        });
      },
      logout: () =>
        set({
          isAuthenticated: false,
          user: null,
          accessToken: null,
          refreshToken: null,
        }),
    }),
    {
      name: "auth-storage",
    },
  ),
);

export default useAuth;
