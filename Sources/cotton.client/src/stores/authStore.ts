import { create } from "zustand";
import api from "../api/http.ts";
import { API_ENDPOINTS } from "../config.ts";

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
  login: () => Promise<void>;
  ensureLogin: () => Promise<void>;
  logout: () => void;
}

export const useAuth = create<AuthState>((set, get) => ({
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
  login: async () => {
    // Fake login endpoint; POST any body
    const res = await api.post<{ token: string }>(
      `${API_ENDPOINTS.auth}/login`,
      { any: "thing" },
    );
    const { token } = res.data;
    set({ token, isAuthenticated: true });
  },
  ensureLogin: async () => {
    if (!get().token) {
      await get().login();
    }
  },
  logout: () => set({ isAuthenticated: false, user: null, token: null }),
}));

export default useAuth;
