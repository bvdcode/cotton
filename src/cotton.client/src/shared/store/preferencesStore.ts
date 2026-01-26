import { create } from "zustand";
import type { ThemeMode } from "../theme";

interface PreferencesState {
  theme: ThemeMode;
  setTheme: (theme: ThemeMode) => void;
}

export const usePreferencesStore = create<PreferencesState>()(
  (set) => ({
    theme: "system",
    setTheme: (theme) => set({ theme }),
  }),
);
