/**
 * Search Results Component
 * 
 * Single Responsibility: Displays list of search results with pagination
 * Open/Closed: Can be extended with different result renderers
 */

import React from 'react';
import {
  Box,
  List,
  Typography,
  Pagination,
  Paper,
  Divider,
  Alert,
  CircularProgress,
} from '@mui/material';
import { useTranslation } from 'react-i18next';
import type { SearchResultDto } from '../../../shared/api/layoutsApi';
import { SearchResultItem } from './SearchResultItem';
import type { NodeDto } from '../../../shared/api/layoutsApi';
import type { NodeFileManifestDto } from '../../../shared/api/nodesApi';

export interface SearchResultsProps {
  /** Search results data */
  results: SearchResultDto | null;
  /** Loading state */
  loading: boolean;
  /** Error state */
  error: string | null;
  /** Current search query */
  query: string;
  /** Callback when page changes */
  onPageChange: (page: number) => void;
  /** Callback when result item is clicked */
  onResultClick?: (item: NodeDto | NodeFileManifestDto, type: 'node' | 'file') => void;
}

/**
 * SearchResults component for displaying paginated search results
 * 
 * Features:
 * - Mixed display of folders and files
 * - Pagination support
 * - Loading and error states
 * - Empty state handling
 */
export const SearchResults: React.FC<SearchResultsProps> = ({
  results,
  loading,
  error,
  query,
  onPageChange,
  onResultClick,
}) => {
  const { t } = useTranslation(['search', 'common']);

  // Loading state
  if (loading) {
    return (
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          minHeight: 400,
        }}
      >
        <CircularProgress />
      </Box>
    );
  }

  // Error state
  if (error) {
    return (
      <Alert severity="error" sx={{ mt: 2 }}>
        {error}
      </Alert>
    );
  }

  // No search performed yet
  if (!results && !query) {
    return (
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          minHeight: 400,
        }}
      >
        <Typography variant="body1" color="text.secondary">
          {t('search.emptyState.initial', 'Enter a search query to find files and folders')}
        </Typography>
      </Box>
    );
  }

  // No results found
  if (results && results.totalCount === 0) {
    return (
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          minHeight: 400,
        }}
      >
        <Typography variant="body1" color="text.secondary">
          {t('search.emptyState.noResults', `No results found for "${query}"`)}
        </Typography>
      </Box>
    );
  }

  if (!results) {
    return null;
  }

  const totalPages = Math.ceil(results.totalCount / results.pageSize);
  const hasMultiplePages = totalPages > 1;

  return (
    <Box sx={{ mt: 3 }}>
      {/* Results header */}
      <Box sx={{ mb: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6" color="text.primary">
          {t('search.resultsHeader', `Found ${results.totalCount} results for "${query}"`)}
        </Typography>
      </Box>

      {/* Results list */}
      <Paper elevation={1}>
        <List disablePadding>
          {/* Render folders first */}
          {results.nodes.map((node, index) => (
            <React.Fragment key={`node-${node.id}`}>
              {index > 0 && <Divider />}
              <SearchResultItem
                item={node}
                type="node"
                onClick={onResultClick}
              />
            </React.Fragment>
          ))}

          {/* Divider between nodes and files if both exist */}
          {results.nodes.length > 0 && results.files.length > 0 && <Divider />}

          {/* Render files */}
          {results.files.map((file, index) => (
            <React.Fragment key={`file-${file.id}`}>
              {(index > 0 || results.nodes.length > 0) && <Divider />}
              <SearchResultItem
                item={file}
                type="file"
                onClick={onResultClick}
              />
            </React.Fragment>
          ))}
        </List>
      </Paper>

      {/* Pagination */}
      {hasMultiplePages && (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 3 }}>
          <Pagination
            count={totalPages}
            page={results.page}
            onChange={(_, page) => onPageChange(page)}
            color="primary"
            size="large"
            showFirstButton
            showLastButton
          />
        </Box>
      )}
    </Box>
  );
};
