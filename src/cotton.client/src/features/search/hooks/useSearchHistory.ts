import { useCallback, useMemo } from "react";
import {
  USER_PREFERENCE_KEYS,
  useUserPreferencesStore,
} from "../../../shared/store/userPreferencesStore";
import {
  addSearchHistoryEntry,
  areSearchHistoryEntriesEqual,
  parseSearchHistoryPreference,
  removeSearchHistoryEntry,
  serializeSearchHistoryPreference,
  type SearchHistoryEntry,
} from "../utils/searchHistory";

interface UseSearchHistoryResult {
  entries: SearchHistoryEntry[];
  addQuery: (query: string) => void;
  removeQuery: (query: string) => void;
  clear: () => void;
}

const readCurrentEntries = (): SearchHistoryEntry[] => {
  const raw =
    useUserPreferencesStore.getState().preferences[
      USER_PREFERENCE_KEYS.searchHistory
    ];
  return parseSearchHistoryPreference(raw);
};

export const useSearchHistory = (): UseSearchHistoryResult => {
  const rawHistory = useUserPreferencesStore(
    (state) => state.preferences[USER_PREFERENCE_KEYS.searchHistory],
  );
  const updatePreferences = useUserPreferencesStore(
    (state) => state.updatePreferences,
  );

  const entries = useMemo(
    () => parseSearchHistoryPreference(rawHistory),
    [rawHistory],
  );

  const saveEntries = useCallback(
    (nextEntries: SearchHistoryEntry[]) => {
      void updatePreferences({
        [USER_PREFERENCE_KEYS.searchHistory]:
          serializeSearchHistoryPreference(nextEntries),
      });
    },
    [updatePreferences],
  );

  const addQuery = useCallback(
    (query: string) => {
      const currentEntries = readCurrentEntries();
      const nextEntries = addSearchHistoryEntry(currentEntries, query);
      if (areSearchHistoryEntriesEqual(currentEntries, nextEntries)) {
        return;
      }

      saveEntries(nextEntries);
    },
    [saveEntries],
  );

  const removeQuery = useCallback(
    (query: string) => {
      const currentEntries = readCurrentEntries();
      const nextEntries = removeSearchHistoryEntry(currentEntries, query);
      if (areSearchHistoryEntriesEqual(currentEntries, nextEntries)) {
        return;
      }

      saveEntries(nextEntries);
    },
    [saveEntries],
  );

  const clear = useCallback(() => {
    if (readCurrentEntries().length === 0) {
      return;
    }

    saveEntries([]);
  }, [saveEntries]);

  return { entries, addQuery, removeQuery, clear };
};
