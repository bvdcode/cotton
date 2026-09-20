import { useCallback, useEffect, useState } from "react";
import {
  layoutsApi,
  type Guid,
  type LayoutSearchResultDto,
} from "../../../shared/api/layoutsApi";
import { useDeepSearchMode } from "../../../features/search/hooks/useDeepSearchMode";

export interface UseLayoutSearchOptions {
  layoutId?: Guid | null;
  initialQuery?: string;
  pageSize?: number;
  debounceMs?: number;
}

export interface UseLayoutSearchState {
  deep: boolean;
  toggleDeep: () => void;
  query: string;
  page: number;
  pageSize: number;
  totalCount: number;
  loading: boolean;
  error: string | null;
  results: LayoutSearchResultDto | null;
  completedQuery: string;

  setQuery: (value: string) => void;
  setPage: (page: number) => void;
  setPageSize: (pageSize: number) => void;
}

export function useLayoutSearch(
  options: UseLayoutSearchOptions,
): UseLayoutSearchState {
  const {
    layoutId,
    initialQuery = "",
    pageSize: initialPageSize = 25,
    debounceMs = 300,
  } = options;

  const [query, setQueryValue] = useState(initialQuery);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(initialPageSize);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [results, setResults] = useState<LayoutSearchResultDto | null>(null);
  const [completedKey, setCompletedKey] = useState("");

  const trimmedQuery = query.trim();
  const {
    deep,
    toggleDeep: toggleMode,
    enableDeep,
  } = useDeepSearchMode((layoutId ?? "") + "\u0000" + trimmedQuery);
  const canSearch = Boolean(layoutId && trimmedQuery);
  const requestKey = JSON.stringify([
    layoutId,
    trimmedQuery,
    deep,
    page,
    pageSize,
  ]);

  const setQuery = useCallback((value: string) => {
    setQueryValue(value);
    setPage(1);
  }, []);

  const toggleDeep = useCallback(() => {
    toggleMode();
    setPage(1);
  }, [toggleMode]);

  useEffect(() => {
    if (!canSearch || !layoutId) {
      return;
    }

    const controller = new AbortController();
    const handle = setTimeout(async () => {
      setError(null);
      setLoading(true);

      try {
        const response = await layoutsApi.search({
          layoutId,
          query: trimmedQuery,
          page,
          pageSize,
          deep,
          signal: controller.signal,
        });

        if (controller.signal.aborted) {
          return;
        }

        if (!deep && page === 1 && response.totalCount === 0) {
          enableDeep();
        }
        setResults(response.data);
        setTotalCount(response.totalCount);
      } catch {
        if (!controller.signal.aborted) {
          setResults(null);
          setTotalCount(0);
          setError(deep ? "smartSearch.error" : "error");
        }
      } finally {
        if (!controller.signal.aborted) {
          setCompletedKey(requestKey);
          setLoading(false);
        }
      }
    }, debounceMs);

    return () => {
      clearTimeout(handle);
      controller.abort();
    };
  }, [
    canSearch,
    debounceMs,
    deep,
    enableDeep,
    layoutId,
    page,
    pageSize,
    requestKey,
    trimmedQuery,
  ]);

  const hasActiveResults = canSearch && completedKey === requestKey;

  return {
    deep,
    toggleDeep,
    query,
    page,
    pageSize,
    totalCount: hasActiveResults ? totalCount : 0,
    loading: canSearch && (loading || !hasActiveResults),
    error: hasActiveResults ? error : null,
    results: hasActiveResults ? results : null,
    completedQuery: hasActiveResults && !error ? trimmedQuery : "",
    setQuery,
    setPage,
    setPageSize,
  };
}
