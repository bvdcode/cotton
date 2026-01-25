import { useEffect, useState } from "react";
import { layoutsApi, type Guid, type LayoutSearchResultDto } from "../../../shared/api/layoutsApi";

export interface UseLayoutSearchOptions {
  layoutId?: Guid | null;
  initialQuery?: string;
  pageSize?: number;
  debounceMs?: number;
}

export interface UseLayoutSearchState {
  query: string;
  pageSize: number;
  totalCount: number;
  loading: boolean;
  error: string | null;
  results: LayoutSearchResultDto | null;

  setQuery: (value: string) => void;
}

export function useLayoutSearch(options: UseLayoutSearchOptions): UseLayoutSearchState {
  const {
    layoutId,
    initialQuery = "",
    pageSize = 100,
    debounceMs = 300,
  } = options;

  const [query, setQuery] = useState(initialQuery);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [results, setResults] = useState<LayoutSearchResultDto | null>(null);

  useEffect(() => {
    // Don't search if no layout or empty query
    if (!layoutId || !query.trim()) {
      setLoading(false);
      setError(null);
      // Keep previous results, don't clear them
      return;
    }

    setError(null);

    const handle = setTimeout(async () => {
      // Set loading true when fetch starts
      setLoading(true);

      try {
        const response = await layoutsApi.search({
          layoutId,
          query: query.trim(),
          page: 1,
          pageSize,
        });

        setResults(response.data);
        setTotalCount(response.totalCount);
      } catch (err) {
        console.error("Failed to search layouts", err);
        setError("searchFailed");
      } finally {
        setLoading(false);
      }
    }, debounceMs);

    return () => {
      clearTimeout(handle);
    };
  }, [layoutId, query, pageSize, debounceMs]);

  return {
    query,
    pageSize,
    totalCount,
    loading,
    error,
    results,
    setQuery,
  };
}
