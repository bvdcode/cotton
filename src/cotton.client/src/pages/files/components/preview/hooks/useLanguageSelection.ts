/**
 * Language Selection Hook
 * 
 * Single Responsibility: Manages programming language selection state
 * Encapsulates language detection and override logic
 */

import { useCallback, useMemo } from "react";
import {
  USER_PREFERENCE_PREFIXES,
  useUserPreferencesStore,
} from "../../../../../shared/store/userPreferencesStore";
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
  const setLanguageOverride = useUserPreferencesStore((s) => s.setLanguageOverride);
  const removeLanguageOverride = useUserPreferencesStore(
    (s) => s.removeLanguageOverride,
  );

  const storedOverride = useUserPreferencesStore(
    useMemo(
      () =>
        (s) =>
          s.preferences[
            `${USER_PREFERENCE_PREFIXES.languageOverride}${fileId}`
          ] ?? null,
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
