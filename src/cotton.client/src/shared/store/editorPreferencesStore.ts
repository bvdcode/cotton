import { create } from "zustand";
import type { EditorMode } from "../../pages/files/components/preview/editors/types";

type EditorPreferencesState = {
  editorModeByFileId: Record<string, EditorMode | undefined>;
  languageOverrideByFileId: Record<string, string | undefined>;

  setEditorMode: (fileId: string, mode: EditorMode) => void;
  clearEditorMode: (fileId: string) => void;

  setLanguageOverride: (fileId: string, language: string) => void;
  clearLanguageOverride: (fileId: string) => void;
};

export const useEditorPreferencesStore = create<EditorPreferencesState>()(
  (set) => ({
    editorModeByFileId: {},
    languageOverrideByFileId: {},

    setEditorMode: (fileId, mode) =>
      set((state) => ({
        editorModeByFileId: { ...state.editorModeByFileId, [fileId]: mode },
      })),

    clearEditorMode: (fileId) =>
      set((state) => {
        if (!(fileId in state.editorModeByFileId)) return state;
        const { [fileId]: _, ...rest } = state.editorModeByFileId;
        return { editorModeByFileId: rest };
      }),

    setLanguageOverride: (fileId, language) =>
      set((state) => ({
        languageOverrideByFileId: {
          ...state.languageOverrideByFileId,
          [fileId]: language,
        },
      })),

    clearLanguageOverride: (fileId) =>
      set((state) => {
        if (!(fileId in state.languageOverrideByFileId)) return state;
        const { [fileId]: _, ...rest } = state.languageOverrideByFileId;
        return { languageOverrideByFileId: rest };
      }),
  }),
);
