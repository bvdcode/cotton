export interface SearchHistoryEntry {
  query: string;
  lastUsedAt: string;
}

export const SEARCH_HISTORY_LIMIT = 10;

const isRecord = (value: unknown): value is Record<string, unknown> =>
  value !== null && typeof value === "object" && !Array.isArray(value);

export const normalizeSearchHistoryQuery = (query: string): string =>
  query.trim().replace(/\s+/g, " ");

const getSearchHistoryKey = (query: string): string =>
  query.toLocaleLowerCase();

const toSearchHistoryEntry = (value: unknown): SearchHistoryEntry | null => {
  if (typeof value === "string") {
    const query = normalizeSearchHistoryQuery(value);
    return query ? { query, lastUsedAt: "" } : null;
  }

  if (!isRecord(value)) {
    return null;
  }

  const rawQuery = value.query;
  if (typeof rawQuery !== "string") {
    return null;
  }

  const query = normalizeSearchHistoryQuery(rawQuery);
  if (!query) {
    return null;
  }

  const rawLastUsedAt = value.lastUsedAt;
  return {
    query,
    lastUsedAt: typeof rawLastUsedAt === "string" ? rawLastUsedAt : "",
  };
};

export const parseSearchHistoryPreference = (
  value: string | undefined,
): SearchHistoryEntry[] => {
  if (!value) return [];

  try {
    const parsed: unknown = JSON.parse(value);
    if (!Array.isArray(parsed)) {
      return [];
    }

    const seen = new Set<string>();
    const entries: SearchHistoryEntry[] = [];

    for (const item of parsed) {
      const entry = toSearchHistoryEntry(item);
      if (!entry) continue;

      const key = getSearchHistoryKey(entry.query);
      if (seen.has(key)) continue;

      seen.add(key);
      entries.push(entry);
    }

    return entries.slice(0, SEARCH_HISTORY_LIMIT);
  } catch {
    return [];
  }
};

export const serializeSearchHistoryPreference = (
  entries: SearchHistoryEntry[],
): string => JSON.stringify(entries.slice(0, SEARCH_HISTORY_LIMIT));

export const areSearchHistoryEntriesEqual = (
  left: SearchHistoryEntry[],
  right: SearchHistoryEntry[],
): boolean => {
  if (left.length !== right.length) return false;

  return left.every(
    (entry, index) =>
      entry.query === right[index]?.query &&
      entry.lastUsedAt === right[index]?.lastUsedAt,
  );
};

export const addSearchHistoryEntry = (
  entries: SearchHistoryEntry[],
  query: string,
  now = new Date(),
): SearchHistoryEntry[] => {
  const normalizedQuery = normalizeSearchHistoryQuery(query);
  if (!normalizedQuery) {
    return entries;
  }

  const nextEntry: SearchHistoryEntry = {
    query: normalizedQuery,
    lastUsedAt: now.toISOString(),
  };
  const nextKey = getSearchHistoryKey(normalizedQuery);
  const dedupedEntries = entries.filter(
    (entry) => getSearchHistoryKey(entry.query) !== nextKey,
  );

  return [nextEntry, ...dedupedEntries].slice(0, SEARCH_HISTORY_LIMIT);
};

export const removeSearchHistoryEntry = (
  entries: SearchHistoryEntry[],
  query: string,
): SearchHistoryEntry[] => {
  const normalizedQuery = normalizeSearchHistoryQuery(query);
  const removeKey = getSearchHistoryKey(normalizedQuery);
  return entries.filter(
    (entry) => getSearchHistoryKey(entry.query) !== removeKey,
  );
};
