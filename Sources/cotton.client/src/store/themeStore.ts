import { create } from "zustand";
import { ThemeMode } from "../types";
import { persist } from "zustand/middleware";
import { getAppPrefix } from "../config/appPrefix";

interface ThemeStore {
  mode: ThemeMode;
  toggleTheme: () => void;
}

export const useThemeStore = create<ThemeStore>()(
  persist(
    (set) => ({
      mode: "dark",
      toggleTheme: () =>
        set((state) => ({ mode: state.mode === "light" ? "dark" : "light" })),
    }),
    {
      name: `${getAppPrefix()}-theme`,
    },
  ),
);
