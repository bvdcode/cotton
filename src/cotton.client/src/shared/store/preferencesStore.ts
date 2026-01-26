import { create } from "zustand";
import type { ThemeMode } from "../theme";
import { persist } from "zustand/middleware";
import { PREFERENCES_STORAGE_KEY } from "../config/storageKeys";

interface EditorPreferences {
  editorModes: Record<string, string>;
  languageOverrides: Record<string, string>;
}

interface PreferencesState {
  theme: ThemeMode;
  setTheme: (theme: ThemeMode) => void;
  editorPreferences: EditorPreferences;
  setEditorMode: (fileId: string, mode: string) => void;
  setLanguageOverride: (fileId: string, language: string) => void;
  removeLanguageOverride: (fileId: string) => void;
}

export const usePreferencesStore = create<PreferencesState>()(
  persist(
    (set) => ({
      theme: "system",
      setTheme: (theme) => set({ theme }),
      editorPreferences: {
        editorModes: {},
        languageOverrides: {},
      },
      setEditorMode: (fileId, mode) =>
        set((state) => ({
          editorPreferences: {
            ...state.editorPreferences,
            editorModes: {
              ...state.editorPreferences.editorModes,
              [fileId]: mode,
            },
          },
        })),
      setLanguageOverride: (fileId, language) =>
        set((state) => ({
          editorPreferences: {
            ...state.editorPreferences,
            languageOverrides: {
              ...state.editorPreferences.languageOverrides,
              [fileId]: language,
            },
          },
        })),
      removeLanguageOverride: (fileId) =>
        set((state) => {
          const { [fileId]: _, ...rest } = state.editorPreferences.languageOverrides;
          return {
            editorPreferences: {
              ...state.editorPreferences,
              languageOverrides: rest,
            },
          };
        }),
    }),
    {
      name: PREFERENCES_STORAGE_KEY,
    },
  ),
);
