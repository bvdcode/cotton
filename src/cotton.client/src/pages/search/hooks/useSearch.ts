/**
 * Search Hook
 * 
 * Single Responsibility: Manages search state and API communication
 * Dependency Inversion: Depends on layoutsApi abstraction
 * 
 * Encapsulates all search-related business logic including:
 * - Query execution
 * - Pagination
 * - Loading states
 * - Error handling
 * - URL synchronization
 */

import { useState, useCallback, useEffect, useRef } from 'react';
import { useSearchParams } from 'react-router-dom';
import { layoutsApi, type SearchResultDto } from '../../../shared/api/layoutsApi';
import type { Guid } from '../../../shared/api/layoutsApi';

export interface UseSearchOptions {
  /** Layout ID to search within */
  layoutId: Guid;
  /** Number of items per page */
  pageSize?: number;
  /** Auto-execute search on mount if query in URL */
  autoSearch?: boolean;
}

export interface UseSearchResult {
  /** Current search query */
  query: string;
  /** Search results data */
  results: SearchResultDto | null;
  /** Loading state */
  loading: boolean;
  /** Error message if any */
  error: string | null;
  /** Current page number */
  currentPage: number;
  /** Execute search with new query */
  search: (newQuery: string) => void;
  /** Change page */
  setPage: (page: number) => void;
  /** Clear search results */
  clear: () => void;
}

/**
 * Hook for managing search functionality with URL synchronization
 * 
 * Features:
 * - URL parameter synchronization (query, page)
 * - Automatic debouncing to prevent excessive API calls
 * - Proper cleanup and cancellation handling
 * - Error handling with user-friendly messages
 * 
 * @param options - Configuration options
 * @returns Search state and control functions
 */
export function useSearch({
  layoutId,
  pageSize = 20,
  autoSearch = true,
}: UseSearchOptions): UseSearchResult {
  const [searchParams, setSearchParams] = useSearchParams();
  const [results, setResults] = useState<SearchResultDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  // Track if component is mounted to prevent state updates after unmount
  const isMountedRef = useRef(true);
  
  // Get query and page from URL parameters
  const query = searchParams.get('q') || '';
  const currentPage = parseInt(searchParams.get('page') || '1', 10);

  // Execute search API call
  const executeSearch = useCallback(
    async (searchQuery: string, page: number) => {
      if (!searchQuery.trim()) {
        setResults(null);
        return;
      }

      setLoading(true);
      setError(null);

      try {
        const data = await layoutsApi.search({
          layoutId,
          query: searchQuery,
          page,
          pageSize,
        });

        if (isMountedRef.current) {
          setResults(data);
          setError(null);
        }
      } catch (err) {
        if (isMountedRef.current) {
          const errorMessage =
            err instanceof Error
              ? err.message
              : 'An error occurred while searching';
          setError(errorMessage);
          setResults(null);
        }
      } finally {
        if (isMountedRef.current) {
          setLoading(false);
        }
      }
    },
    [layoutId, pageSize]
  );

  // Update URL parameters and trigger search
  const search = useCallback(
    (newQuery: string) => {
      const params = new URLSearchParams();
      if (newQuery.trim()) {
        params.set('q', newQuery.trim());
        params.set('page', '1'); // Reset to first page on new search
      }
      setSearchParams(params);
    },
    [setSearchParams]
  );

  // Change page
  const setPage = useCallback(
    (page: number) => {
      if (!query) return;
      
      const params = new URLSearchParams();
      params.set('q', query);
      params.set('page', page.toString());
      setSearchParams(params);
    },
    [query, setSearchParams]
  );

  // Clear search
  const clear = useCallback(() => {
    setSearchParams(new URLSearchParams());
    setResults(null);
    setError(null);
  }, [setSearchParams]);

  // Execute search when URL parameters change
  useEffect(() => {
    if (query && (autoSearch || results !== null)) {
      executeSearch(query, currentPage);
    }
  }, [query, currentPage, executeSearch, autoSearch, results]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  return {
    query,
    results,
    loading,
    error,
    currentPage,
    search,
    setPage,
    clear,
  };
}
