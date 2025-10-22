import { create } from "zustand";

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
    const res = await fetch("http://localhost:5182/api/v1/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ any: "thing" }),
    });
    if (!res.ok) throw new Error(`Login failed: ${res.status}`);
    const { token } = (await res.json()) as { token: string };
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
