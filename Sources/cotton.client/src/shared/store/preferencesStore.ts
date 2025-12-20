import { create } from "zustand";
import type { ThemeMode } from "../theme";
import { persist } from "zustand/middleware";
import { PREFERENCES_STORAGE_KEY } from "../config/storageKeys";

interface PreferencesState {
  theme: ThemeMode;
  setTheme: (theme: ThemeMode) => void;
}

export const usePreferencesStore = create<PreferencesState>()(
  persist(
    (set) => ({
      theme: "system",
      setTheme: (theme) => set({ theme }),
    }),
    {
      name: PREFERENCES_STORAGE_KEY,
    },
  ),
);
