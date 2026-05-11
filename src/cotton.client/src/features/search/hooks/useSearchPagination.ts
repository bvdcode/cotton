import { useCallback, useEffect, useRef, useState } from "react";
import {
  layoutsApi,
  type LayoutSearchResultDto,
} from "../../../shared/api/layoutsApi";
import { mergeSearchResults } from "../utils/normalizeSearch";

const SEARCH_PAGE_SIZE = 80;
const SEARCH_DEBOUNCE_MS = 260;

interface UseSearchPaginationOptions {
  trimmedQuery: string;
  layoutId: string | undefined;
}

export interface SearchPaginationState {
  debouncedQuery: string;
  results: LayoutSearchResultDto | null;
  totalCount: number;
  loadingInitial: boolean;
  loadingMore: boolean;
  error: string | null;
  loadNextPage: () => void;
}

export const useSearchPagination = ({
  trimmedQuery,
  layoutId,
}: UseSearchPaginationOptions): SearchPaginationState => {
  const searchGenerationRef = useRef(0);
  const requestedPageRef = useRef(0);

  const [debouncedQuery, setDebouncedQuery] = useState("");
  const [results, setResults] = useState<LayoutSearchResultDto | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [loadedPage, setLoadedPage] = useState(0);
  const [loadingInitial, setLoadingInitial] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!trimmedQuery) {
      setDebouncedQuery("");
      return;
    }

    const handle = window.setTimeout(() => {
      setDebouncedQuery(trimmedQuery);
    }, SEARCH_DEBOUNCE_MS);

    return () => window.clearTimeout(handle);
  }, [trimmedQuery]);

  const fetchSearchPage = useCallback(
    async (
      pageToLoad: number,
      mode: "replace" | "append",
      generation = searchGenerationRef.current,
    ) => {
      if (!layoutId || !debouncedQuery) return;

      setError(null);
      if (mode === "replace") {
        setLoadingInitial(true);
      } else {
        setLoadingMore(true);
      }

      try {
        const response = await layoutsApi.search({
          layoutId,
          query: debouncedQuery,
          page: pageToLoad,
          pageSize: SEARCH_PAGE_SIZE,
        });

        if (generation !== searchGenerationRef.current) return;

        setResults((previous) =>
          mode === "replace"
            ? response.data
            : mergeSearchResults(previous, response.data),
        );
        setTotalCount(response.totalCount);
        setLoadedPage(pageToLoad);
      } catch (err) {
        if (generation !== searchGenerationRef.current) return;
        requestedPageRef.current = Math.max(0, pageToLoad - 1);
        console.error("Failed to search layouts", err);
        setError("searchFailed");
      } finally {
        if (generation === searchGenerationRef.current) {
          if (mode === "replace") {
            setLoadingInitial(false);
          } else {
            setLoadingMore(false);
          }
        }
      }
    },
    [debouncedQuery, layoutId],
  );

  useEffect(() => {
    const generation = searchGenerationRef.current + 1;
    searchGenerationRef.current = generation;
    setResults(null);
    setTotalCount(0);
    setLoadedPage(0);
    requestedPageRef.current = 0;
    setLoadingInitial(false);
    setLoadingMore(false);
    setError(null);

    if (!layoutId || !debouncedQuery) return;

    requestedPageRef.current = 1;
    void fetchSearchPage(1, "replace", generation);
  }, [debouncedQuery, fetchSearchPage, layoutId]);

  const loadedContentCount =
    (results?.nodes?.length ?? 0) + (results?.files?.length ?? 0);
  const hasMoreContent =
    debouncedQuery.length > 0 && loadedContentCount < totalCount;

  const loadNextPage = useCallback(() => {
    if (!hasMoreContent || loadingInitial || loadingMore || loadedPage <= 0) {
      return;
    }

    const nextPage = loadedPage + 1;
    if (requestedPageRef.current >= nextPage) {
      return;
    }

    requestedPageRef.current = nextPage;
    void fetchSearchPage(nextPage, "append");
  }, [
    fetchSearchPage,
    hasMoreContent,
    loadedPage,
    loadingInitial,
    loadingMore,
  ]);

  return {
    debouncedQuery,
    results,
    totalCount,
    loadingInitial,
    loadingMore,
    error,
    loadNextPage,
  };
};
