import { create } from "zustand";
import type { ThemeMode } from "../theme";
import { persist } from "zustand/middleware";
import { PREFERENCES_STORAGE_KEY } from "../config/storageKeys";
import type { InterfaceLayoutType } from "../api/layoutsApi";

interface EditorPreferences {
  editorModes: Record<string, string>;
  languageOverrides: Record<string, string>;
}

interface LayoutPreferences {
  filesLayoutType?: InterfaceLayoutType;
  trashLayoutType?: InterfaceLayoutType;
  filesTilesSize?: "small" | "medium" | "large";
  trashTilesSize?: "small" | "medium" | "large";
  fileListColumnWidths?: Record<string, number>;
}

interface PreferencesState {
  theme: ThemeMode;
  setTheme: (theme: ThemeMode) => void;
  editorPreferences: EditorPreferences;
  setEditorMode: (fileId: string, mode: string) => void;
  setLanguageOverride: (fileId: string, language: string) => void;
  removeLanguageOverride: (fileId: string) => void;
  layoutPreferences: LayoutPreferences;
  setFilesLayoutType: (layoutType: InterfaceLayoutType) => void;
  setTrashLayoutType: (layoutType: InterfaceLayoutType) => void;
  setFilesTilesSize: (size: "small" | "medium" | "large") => void;
  setTrashTilesSize: (size: "small" | "medium" | "large") => void;
  setFileListColumnWidth: (field: string, width: number) => void;
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
      layoutPreferences: {},
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
      setFilesLayoutType: (layoutType) =>
        set((state) => ({
          layoutPreferences: {
            ...state.layoutPreferences,
            filesLayoutType: layoutType,
          },
        })),
      setTrashLayoutType: (layoutType) =>
        set((state) => ({
          layoutPreferences: {
            ...state.layoutPreferences,
            trashLayoutType: layoutType,
          },
        })),
      setFilesTilesSize: (size) =>
        set((state) => ({
          layoutPreferences: {
            ...state.layoutPreferences,
            filesTilesSize: size,
          },
        })),
      setTrashTilesSize: (size) =>
        set((state) => ({
          layoutPreferences: {
            ...state.layoutPreferences,
            trashTilesSize: size,
          },
        })),
      setFileListColumnWidth: (field, width) =>
        set((state) => ({
          layoutPreferences: {
            ...state.layoutPreferences,
            fileListColumnWidths: {
              ...state.layoutPreferences.fileListColumnWidths,
              [field]: width,
            },
          },
        })),
    }),
    {
      name: PREFERENCES_STORAGE_KEY,
    },
  ),
);
