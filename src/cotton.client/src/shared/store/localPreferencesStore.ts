import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import type { InterfaceLayoutType } from "../api/layoutsApi";
import { STORAGE_KEY_PREFIX } from "../config/storageKeys";

type TilesSize = "small" | "medium" | "large";

const DEFAULT_TILES_SIZE: TilesSize = "medium";

interface LocalPreferencesState {
  filesLayoutType: InterfaceLayoutType | null;
  trashLayoutType: InterfaceLayoutType | null;
  filesTilesSize: TilesSize;
  trashTilesSize: TilesSize;

  gallerySmoothTransitions: boolean;
  galleryPreferPreview: boolean;

  editorModes: Record<string, string>;
  languageOverrides: Record<string, string>;

  setFilesLayoutType: (layoutType: InterfaceLayoutType) => void;
  setTrashLayoutType: (layoutType: InterfaceLayoutType) => void;
  setFilesTilesSize: (size: TilesSize) => void;
  setTrashTilesSize: (size: TilesSize) => void;

  setGallerySmoothTransitions: (enabled: boolean) => void;
  setGalleryPreferPreview: (enabled: boolean) => void;

  setEditorMode: (fileId: string, mode: string) => void;
  setLanguageOverride: (fileId: string, language: string) => void;
  removeLanguageOverride: (fileId: string) => void;

  reset: () => void;
}

const INITIAL_STATE = {
  filesLayoutType: null as InterfaceLayoutType | null,
  trashLayoutType: null as InterfaceLayoutType | null,
  filesTilesSize: DEFAULT_TILES_SIZE as TilesSize,
  trashTilesSize: DEFAULT_TILES_SIZE as TilesSize,
  gallerySmoothTransitions: true,
  galleryPreferPreview: true,
  editorModes: {} as Record<string, string>,
  languageOverrides: {} as Record<string, string>,
};

export const useLocalPreferencesStore = create<LocalPreferencesState>()(
  persist(
    (set) => ({
      ...INITIAL_STATE,

      setFilesLayoutType: (layoutType) => set({ filesLayoutType: layoutType }),
      setTrashLayoutType: (layoutType) => set({ trashLayoutType: layoutType }),
      setFilesTilesSize: (size) => set({ filesTilesSize: size }),
      setTrashTilesSize: (size) => set({ trashTilesSize: size }),

      setGallerySmoothTransitions: (enabled) =>
        set({ gallerySmoothTransitions: enabled }),

      setGalleryPreferPreview: (enabled) =>
        set({ galleryPreferPreview: enabled }),

      setEditorMode: (fileId, mode) =>
        set((s) => ({
          editorModes: { ...s.editorModes, [fileId]: mode },
        })),

      setLanguageOverride: (fileId, language) =>
        set((s) => ({
          languageOverrides: { ...s.languageOverrides, [fileId]: language },
        })),

      removeLanguageOverride: (fileId) =>
        set((s) => {
          const next = { ...s.languageOverrides };
          delete next[fileId];
          return { languageOverrides: next };
        }),

      reset: () => set(INITIAL_STATE),
    }),
    {
      name: `${STORAGE_KEY_PREFIX}local-prefs`,
      storage: createJSONStorage(() => sessionStorage),
    },
  ),
);

export const selectFilesLayoutType = (
  s: LocalPreferencesState,
): InterfaceLayoutType | null => s.filesLayoutType;

export const selectTrashLayoutType = (
  s: LocalPreferencesState,
): InterfaceLayoutType | null => s.trashLayoutType;

export const selectFilesTilesSize = (s: LocalPreferencesState): TilesSize =>
  s.filesTilesSize;

export const selectTrashTilesSize = (s: LocalPreferencesState): TilesSize =>
  s.trashTilesSize;

export const selectGallerySmoothTransitions = (
  s: LocalPreferencesState,
): boolean => s.gallerySmoothTransitions;

export const selectGalleryPreferPreview = (
  s: LocalPreferencesState,
): boolean => s.galleryPreferPreview;
