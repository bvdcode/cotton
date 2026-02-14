/**
 * Language Selection Hook
 * 
 * Single Responsibility: Manages programming language selection state
 * Encapsulates language detection and override logic
 */

import { useCallback, useMemo } from "react";
import {
  selectLanguageOverrides,
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
  const overrides = useUserPreferencesStore(selectLanguageOverrides);
  const setLanguageOverride = useUserPreferencesStore((s) => s.setLanguageOverride);
  const removeLanguageOverride = useUserPreferencesStore(
    (s) => s.removeLanguageOverride,
  );

  const language = useMemo(() => {
    const stored = overrides[fileId];
    if (stored) return stored;
    return detectMonacoLanguageFromFileName(fileName);
  }, [fileId, fileName, overrides]);

  const setLanguage = useCallback((newLanguage: string) => {
    setLanguageOverride(fileId, newLanguage);
  }, [fileId, setLanguageOverride]);

  const resetLanguage = useCallback(() => {
    removeLanguageOverride(fileId);
  }, [fileId, removeLanguageOverride]);

  return { language, setLanguage, resetLanguage };
}
