import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { UserRole } from "../../auth/types";
import { useAuthStore } from "../../../shared/store/authStore";
import type { SearchDictionaryEntry, SearchSettingRow } from "../types";
import {
  isDictionaryEntry,
  normalizeCompactSearchText,
  normalizeSearchText,
} from "../utils/normalizeSearch";

const MIN_SETTING_QUERY_LENGTH = 3;

type DictionaryMatch = { row: SearchSettingRow; score: number };

export const useDictionaryMatch = (debouncedQuery: string): SearchSettingRow[] => {
  const { t } = useTranslation("search");
  const userRole = useAuthStore((s) => s.user?.role ?? null);
  const rawDictionary = t("dictionary", { returnObjects: true }) as unknown;

  const dictionaryEntries = useMemo<SearchDictionaryEntry[]>(() => {
    const entries = Array.isArray(rawDictionary)
      ? rawDictionary.filter(isDictionaryEntry)
      : [];

    return entries.filter(
      (entry) => !entry.adminOnly || userRole === UserRole.Admin,
    );
  }, [rawDictionary, userRole]);

  return useMemo(() => {
    const normalizedQuery = normalizeSearchText(debouncedQuery);
    const compactQuery = normalizeCompactSearchText(debouncedQuery);
    if (normalizedQuery.length < MIN_SETTING_QUERY_LENGTH) return [];

    return dictionaryEntries
      .map<DictionaryMatch | null>((entry) => {
        const normalizedTitle = normalizeSearchText(entry.title);
        const normalizedKeywords = entry.keywords.map(normalizeSearchText);
        const normalizedDescription = normalizeSearchText(
          entry.description ?? "",
        );
        const haystack = [
          normalizedTitle,
          normalizedDescription,
          normalizeSearchText(entry.path),
          ...normalizedKeywords,
        ].join(" ");
        const compactKeywords = entry.keywords.map(normalizeCompactSearchText);
        const compactHaystack = [
          normalizeCompactSearchText(entry.title),
          normalizeCompactSearchText(entry.description ?? ""),
          normalizeCompactSearchText(entry.path),
          ...compactKeywords,
        ].join(" ");

        const matchesCompact =
          compactQuery.length > 0 && compactHaystack.includes(compactQuery);

        if (!haystack.includes(normalizedQuery) && !matchesCompact) {
          return null;
        }

        const keywordStarts = normalizedKeywords.some((keyword) =>
          keyword.startsWith(normalizedQuery),
        );
        const compactKeywordStarts =
          compactQuery.length > 0 &&
          compactKeywords.some((keyword) => keyword.startsWith(compactQuery));
        const score = normalizedTitle.startsWith(normalizedQuery)
          ? 0
          : keywordStarts || compactKeywordStarts
            ? 1
            : normalizedTitle.includes(normalizedQuery) ||
                (compactQuery.length > 0 &&
                  normalizeCompactSearchText(entry.title).includes(
                    compactQuery,
                  ))
              ? 2
              : 3;

        return {
          row: {
            id: `setting-${entry.id}`,
            kind: "setting" as const,
            entry,
          },
          score,
        };
      })
      .filter((match): match is DictionaryMatch => Boolean(match))
      .sort((a, b) => a.score - b.score || a.row.id.localeCompare(b.row.id))
      .map((match) => match.row);
  }, [debouncedQuery, dictionaryEntries]);
};
