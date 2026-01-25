import { useCallback, useState } from "react";
import { layoutsApi, type Guid, type LayoutSearchResultDto } from "../../../shared/api/layoutsApi";

export interface UseLayoutSearchOptions {
  layoutId: Guid;
  initialQuery?: string;
  pageSize?: number;
}

export interface UseLayoutSearchState {
  query: string;
  page: number;
  pageSize: number;
  totalCount: number;
  loading: boolean;
  error: string | null;
  results: LayoutSearchResultDto | null;

  setQuery: (value: string) => void;
  search: (override?: { query?: string; page?: number }) => Promise<void>;
}

export function useLayoutSearch(options: UseLayoutSearchOptions): UseLayoutSearchState {
  const { layoutId, initialQuery = "", pageSize = 20 } = options;

  const [query, setQuery] = useState(initialQuery);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [results, setResults] = useState<LayoutSearchResultDto | null>(null);

  const search = useCallback(
    async (override?: { query?: string; page?: number }) => {
      const effectiveQuery = override?.query ?? query;
      const effectivePage = override?.page ?? page;

      if (!effectiveQuery.trim()) {
        setError(null);
        setResults(null);
        setTotalCount(0);
        return;
      }

      try {
        setLoading(true);
        setError(null);

        const response = await layoutsApi.search({
          layoutId,
          query: effectiveQuery,
          page: effectivePage,
          pageSize,
        });

        setResults(response.data);
        setTotalCount(response.totalCount);
        setPage(effectivePage);
      } catch (err) {
        console.error("Failed to search layouts", err);
        setError("Failed to search");
      } finally {
        setLoading(false);
      }
    },
    [layoutId, page, pageSize, query],
  );

  return {
    query,
    page,
    pageSize,
    totalCount,
    loading,
    error,
    results,
    setQuery,
    search,
  };
}
