import { useCallback, useEffect, useMemo, useRef, useState } from "react";
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

type SearchDataState = {
  key: string;
  results: LayoutSearchResultDto | null;
  totalCount: number;
  loadedPage: number;
  loadingInitial: boolean;
  loadingMore: boolean;
  error: string | null;
};

const createEmptySearchDataState = (key: string): SearchDataState => ({
  key,
  results: null,
  totalCount: 0,
  loadedPage: 0,
  loadingInitial: false,
  loadingMore: false,
  error: null,
});

const buildSearchKey = (
  layoutId: string | undefined,
  query: string,
): string => {
  return layoutId && query ? layoutId + "\u0000" + query : "";
};

export const useSearchPagination = ({
  trimmedQuery,
  layoutId,
}: UseSearchPaginationOptions): SearchPaginationState => {
  const searchGenerationRef = useRef(0);
  const requestedPageRef = useRef(0);

  const [debouncedQueryValue, setDebouncedQueryValue] = useState(trimmedQuery);
  const debouncedQuery = trimmedQuery.length > 0 ? debouncedQueryValue : "";
  const activeSearchKey = useMemo(
    () => buildSearchKey(layoutId, debouncedQuery),
    [debouncedQuery, layoutId],
  );
  const [searchDataState, setSearchDataState] = useState<SearchDataState>(() =>
    createEmptySearchDataState(activeSearchKey),
  );
  const isInitialSearchPending =
    activeSearchKey.length > 0 && searchDataState.key !== activeSearchKey;
  const searchData =
    searchDataState.key === activeSearchKey
      ? searchDataState
      : {
          ...createEmptySearchDataState(activeSearchKey),
          loadingInitial: isInitialSearchPending,
        };

  useEffect(() => {
    if (!trimmedQuery) {
      return;
    }

    const handle = window.setTimeout(() => {
      setDebouncedQueryValue(trimmedQuery);
    }, SEARCH_DEBOUNCE_MS);

    return () => window.clearTimeout(handle);
  }, [trimmedQuery]);

  const fetchSearchPage = useCallback(
    async (
      pageToLoad: number,
      mode: "replace" | "append",
      key = activeSearchKey,
      generation = searchGenerationRef.current,
    ) => {
      if (!layoutId || !debouncedQuery || !key) return;

      setSearchDataState((previous) => {
        const current =
          previous.key === key ? previous : createEmptySearchDataState(key);
        return {
          ...current,
          error: null,
          loadingInitial: mode === "replace" ? true : current.loadingInitial,
          loadingMore: mode === "append" ? true : current.loadingMore,
        };
      });

      try {
        const response = await layoutsApi.search({
          layoutId,
          query: debouncedQuery,
          page: pageToLoad,
          pageSize: SEARCH_PAGE_SIZE,
        });

        if (generation !== searchGenerationRef.current) return;

        setSearchDataState((previous) => {
          const current =
            previous.key === key ? previous : createEmptySearchDataState(key);
          return {
            ...current,
            results:
              mode === "replace"
                ? response.data
                : mergeSearchResults(current.results, response.data),
            totalCount: response.totalCount,
            loadedPage: pageToLoad,
            loadingInitial: false,
            loadingMore: false,
            error: null,
          };
        });
      } catch (err) {
        if (generation !== searchGenerationRef.current) return;
        requestedPageRef.current = Math.max(0, pageToLoad - 1);
        console.error("Failed to search layouts", err);
        setSearchDataState((previous) => {
          const current =
            previous.key === key ? previous : createEmptySearchDataState(key);
          return {
            ...current,
            loadingInitial: false,
            loadingMore: false,
            error: "searchFailed",
          };
        });
      }
    },
    [activeSearchKey, debouncedQuery, layoutId],
  );

  useEffect(() => {
    const generation = searchGenerationRef.current + 1;
    searchGenerationRef.current = generation;
    requestedPageRef.current = 0;

    if (!activeSearchKey) return;

    requestedPageRef.current = 1;
    void fetchSearchPage(1, "replace", activeSearchKey, generation);
  }, [activeSearchKey, fetchSearchPage]);

  const loadedContentCount =
    (searchData.results?.nodes?.length ?? 0) +
    (searchData.results?.files?.length ?? 0);
  const hasMoreContent =
    debouncedQuery.length > 0 && loadedContentCount < searchData.totalCount;

  const loadNextPage = useCallback(() => {
    if (
      !hasMoreContent ||
      searchData.loadingInitial ||
      searchData.loadingMore ||
      searchData.loadedPage <= 0
    ) {
      return;
    }

    const nextPage = searchData.loadedPage + 1;
    if (requestedPageRef.current >= nextPage) {
      return;
    }

    requestedPageRef.current = nextPage;
    void fetchSearchPage(nextPage, "append");
  }, [
    fetchSearchPage,
    hasMoreContent,
    searchData.loadedPage,
    searchData.loadingInitial,
    searchData.loadingMore,
  ]);

  return {
    debouncedQuery,
    results: searchData.results,
    totalCount: searchData.totalCount,
    loadingInitial: searchData.loadingInitial,
    loadingMore: searchData.loadingMore,
    error: searchData.error,
    loadNextPage,
  };
};
