/**
 * Language Selection Hook
 * 
 * Single Responsibility: Manages programming language selection state
 * Encapsulates language detection and override logic
 */

import { useCallback, useMemo } from "react";
import {
  useLocalPreferencesStore,
} from "../../../../../shared/store/localPreferencesStore";
import { detectMonacoLanguageFromFileName } from "../../../../../shared/utils/languageDetection";

interface UseLanguageSelectionOptions {
  fileName: string;
  fileId: string;
}

interface UseLanguageSelectionResult {
  language: string;
  setLanguage: (language: string) => void;
  resetLanguage: () => void;
}

/**
 * Hook for managing language selection with server-backed preferences.
 */
export function useLanguageSelection({
  fileName,
  fileId,
}: UseLanguageSelectionOptions): UseLanguageSelectionResult {
  const setLanguageOverride = useLocalPreferencesStore((s) => s.setLanguageOverride);
  const removeLanguageOverride = useLocalPreferencesStore(
    (s) => s.removeLanguageOverride,
  );

  const storedOverride = useLocalPreferencesStore(
    useMemo(
      () =>
        (s) =>
          s.languageOverrides[fileId] ?? null,
      [fileId],
    ),
  );

  const language = useMemo(() => {
    if (storedOverride && storedOverride.trim().length > 0) return storedOverride;
    return detectMonacoLanguageFromFileName(fileName);
  }, [fileName, storedOverride]);

  const setLanguage = useCallback((newLanguage: string) => {
    setLanguageOverride(fileId, newLanguage);
  }, [fileId, setLanguageOverride]);

  const resetLanguage = useCallback(() => {
    removeLanguageOverride(fileId);
  }, [fileId, removeLanguageOverride]);

  return { language, setLanguage, resetLanguage };
}
