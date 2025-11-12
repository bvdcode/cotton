import { create } from "zustand";
import api from "../api/http.ts";
import { getMe } from "../api/users.ts";
import { API_ENDPOINTS } from "../config.ts";
import { persist } from "zustand/middleware";

export interface AuthUser {
  id: string;
  username: string;
  createdAt: string;
  updatedAt: string;
}

interface AuthState {
  isAuthenticated: boolean;
  user: AuthUser | null;
  accessToken: string | null;
  refreshToken: string | null;
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
      login: async (username: string, password: string) => {
        // Login endpoint with username and password
        const res = await api.post<{
          accessToken: string;
          refreshToken: string;
        }>(`${API_ENDPOINTS.auth}/login`, { username, password });
        const { accessToken, refreshToken } = res.data;

        // Get user info from /api/v1/users/me
        const user = await getMe();

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

        // Get updated user info
        const user = await getMe();

        set({
          accessToken: newAccessToken,
          refreshToken: newRefreshToken,
          user,
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
