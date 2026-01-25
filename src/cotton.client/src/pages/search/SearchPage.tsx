/**
 * Search Page Component
 * 
 * Single Responsibility: Orchestrates search functionality
 * Open/Closed: Can be extended with filters and advanced search
 * Dependency Inversion: Depends on useSearch hook abstraction
 * 
 * Main page for searching files and folders within the layout.
 * Features:
 * - Search bar with real-time query updates
 * - Paginated results display
 * - Mixed results (folders and files)
 * - URL-synchronized state
 */

import React, { useEffect } from 'react';
import { Box, Container, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { SearchBar, SearchResults } from './components';
import { useSearch } from './hooks';
import { useLayoutsStore } from '../../shared/store/layoutsStore';
import Loader from '../../shared/ui/Loader';

/**
 * SearchPage - Main search interface
 * 
 * Coordinates search bar and results display with proper state management
 */
export const SearchPage: React.FC = () => {
  const { t } = useTranslation(['search', 'common']);
  const { rootNode, loadingRoot, error: layoutError, ensureHomeData } = useLayoutsStore();

  // Ensure we have root layout data
  useEffect(() => {
    void ensureHomeData();
  }, [ensureHomeData]);

  // Get layout ID from root node
  const layoutId = rootNode?.layoutId;

  // Search hook with all business logic
  const {
    query,
    results,
    totalCount,
    loading,
    error,
    currentPage,
    search,
    setPage,
    clear,
  } = useSearch({
    layoutId: layoutId || '',
    pageSize: 20,
    autoSearch: true,
  });

  // Show loader while loading layout data
  if (loadingRoot) {
    return (
      <Loader
        title={t('search:loading.title')}
        caption={t('search:loading.caption')}
      />
    );
  }

  // Handle layout loading error
  if (layoutError || !layoutId) {
    return (
      <Container maxWidth="lg">
        <Box sx={{ py: 4 }}>
          <Typography variant="h5" color="error">
            {layoutError || t('search:errors.noLayout')}
          </Typography>
        </Box>
      </Container>
    );
  }

  return (
    <Container maxWidth="lg">
      <Box sx={{ py: 4 }}>
        {/* Search Bar */}
        <SearchBar
          value={query}
          onSearch={search}
          onClear={clear}
          loading={loading}
        />

        {/* Search Results */}
        <SearchResults
          results={results}
          totalCount={totalCount}
          currentPage={currentPage}
          pageSize={20}
          loading={loading}
          error={error}
          query={query}
          onPageChange={setPage}
        />
      </Box>
    </Container>
  );
};
