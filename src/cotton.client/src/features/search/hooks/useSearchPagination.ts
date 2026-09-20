import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  layoutsApi,
  type LayoutSearchResultDto,
} from "../../../shared/api/layoutsApi";
import { mergeSearchResults } from "../utils/normalizeSearch";
import { useDeepSearchMode } from "./useDeepSearchMode";

const SEARCH_PAGE_SIZE = 80;
const SEARCH_DEBOUNCE_MS = 260;

interface UseSearchPaginationOptions {
  trimmedQuery: string;
  layoutId: string | undefined;
}

export interface SearchPaginationState {
  deep: boolean;
  toggleDeep: () => void;
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
  const abortControllerRef = useRef<AbortController | null>(null);
  const { deep, toggleDeep, enableDeep } = useDeepSearchMode(
    buildSearchKey(layoutId, trimmedQuery),
  );

  const [debouncedQueryValue, setDebouncedQueryValue] = useState(trimmedQuery);
  const debouncedQuery =
    trimmedQuery === debouncedQueryValue ? debouncedQueryValue : "";
  const activeSearchKey = useMemo(
    () =>
      debouncedQuery
        ? buildSearchKey(layoutId, debouncedQuery) + "\u0000" + deep
        : "",
    [debouncedQuery, deep, layoutId],
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
      const signal = abortControllerRef.current?.signal;
      if (!layoutId || !debouncedQuery || !key || !signal || signal.aborted) {
        return;
      }

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
          deep,
          signal,
        });

        if (signal.aborted || generation !== searchGenerationRef.current) {
          return;
        }

        if (!deep && pageToLoad === 1 && response.totalCount === 0) {
          enableDeep();
        }

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
      } catch {
        if (signal.aborted || generation !== searchGenerationRef.current) {
          return;
        }
        requestedPageRef.current = Math.max(0, pageToLoad - 1);
        setSearchDataState((previous) => {
          const current =
            previous.key === key ? previous : createEmptySearchDataState(key);
          return {
            ...current,
            loadingInitial: false,
            loadingMore: false,
            error: deep ? "smartSearch.error" : "error",
          };
        });
      }
    },
    [activeSearchKey, debouncedQuery, deep, enableDeep, layoutId],
  );

  useEffect(() => {
    const generation = searchGenerationRef.current + 1;
    searchGenerationRef.current = generation;
    requestedPageRef.current = 0;

    if (!activeSearchKey) {
      return;
    }

    const controller = new AbortController();
    abortControllerRef.current = controller;
    requestedPageRef.current = 1;
    void fetchSearchPage(1, "replace", activeSearchKey, generation);
    return () => controller.abort();
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
    deep,
    toggleDeep,
    debouncedQuery,
    results: searchData.results,
    totalCount: searchData.totalCount,
    loadingInitial: searchData.loadingInitial,
    loadingMore: searchData.loadingMore,
    error: searchData.error,
    loadNextPage,
  };
};
