import { useEffect, useState } from "react";
import {
  layoutsApi,
  type Guid,
  type LayoutSearchResultDto,
} from "../../../shared/api/layoutsApi";

export interface UseLayoutSearchOptions {
  layoutId?: Guid | null;
  initialQuery?: string;
  pageSize?: number;
  debounceMs?: number;
}

export interface UseLayoutSearchState {
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

  const [query, setQuery] = useState(initialQuery);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(initialPageSize);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [results, setResults] = useState<LayoutSearchResultDto | null>(null);
  const [completedQuery, setCompletedQuery] = useState("");

  const trimmedQuery = query.trim();
  const canSearch = Boolean(layoutId && trimmedQuery);

  useEffect(() => {
    if (!canSearch || !layoutId) {
      return;
    }

    const handle = setTimeout(async () => {
      setError(null);
      setLoading(true);

      try {
        const response = await layoutsApi.search({
          layoutId,
          query: trimmedQuery,
          page,
          pageSize,
        });

        setResults(response.data);
        setTotalCount(response.totalCount);
        setCompletedQuery(trimmedQuery);
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
  }, [canSearch, debounceMs, layoutId, page, pageSize, trimmedQuery]);

  const hasActiveResults = canSearch && completedQuery === trimmedQuery;

  return {
    query,
    page,
    pageSize,
    totalCount: hasActiveResults ? totalCount : 0,
    loading: canSearch ? loading : false,
    error: canSearch ? error : null,
    results: hasActiveResults ? results : null,
    completedQuery: hasActiveResults ? completedQuery : "",
    setQuery,
    setPage,
    setPageSize,
  };
}
