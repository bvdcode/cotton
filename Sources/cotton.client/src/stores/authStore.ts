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
  login: (user?: Partial<AuthUser>) => void;
  logout: () => void;
}

export const useAuth = create<AuthState>((set) => ({
  // Mocked defaults: authenticated as Admin
  isAuthenticated: true,
  user: { id: "1", name: "Mock User", role: "Admin" },
  login: (user) =>
    set({
      isAuthenticated: true,
      user: {
        id: user?.id ?? "1",
        name: user?.name ?? "Mock User",
        role: (user?.role as Role) ?? "Admin",
      },
    }),
  logout: () => set({ isAuthenticated: false, user: null }),
}));

export default useAuth;
