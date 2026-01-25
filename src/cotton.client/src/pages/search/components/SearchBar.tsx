/**
 * Search Bar Component
 * 
 * Single Responsibility: Provides search input interface
 * Open/Closed: Can be extended with filters without modification
 */

import React, { useState, useCallback } from 'react';
import {
  TextField,
  InputAdornment,
  IconButton,
  Paper,
  Box,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import { useTranslation } from 'react-i18next';

export interface SearchBarProps {
  /** Current search query */
  value: string;
  /** Callback when search is executed */
  onSearch: (query: string) => void;
  /** Callback when search is cleared */
  onClear?: () => void;
  /** Loading state */
  loading?: boolean;
  /** Placeholder text override */
  placeholder?: string;
}

/**
 * SearchBar component for entering and executing search queries
 * 
 * Features:
 * - Real-time search on Enter key
 * - Clear button to reset search
 * - Loading state support
 * - Internationalization ready
 */
export const SearchBar: React.FC<SearchBarProps> = ({
  value,
  onSearch,
  onClear,
  loading = false,
  placeholder,
}) => {
  const { t } = useTranslation(['search', 'common']);
  const [localValue, setLocalValue] = useState(value);

  const handleSubmit = useCallback((e: React.FormEvent) => {
    e.preventDefault();
    if (localValue.trim()) {
      onSearch(localValue.trim());
    }
  }, [localValue, onSearch]);

  const handleClear = useCallback(() => {
    setLocalValue('');
    onClear?.();
  }, [onClear]);

  const handleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setLocalValue(e.target.value);
  }, []);

  // Update local value when prop value changes (e.g., from URL)
  React.useEffect(() => {
    setLocalValue(value);
  }, [value]);

  return (
    <Paper
      component="form"
      onSubmit={handleSubmit}
      elevation={2}
      sx={{
        display: 'flex',
        alignItems: 'center',
        width: '100%',
      }}
    >
      <Box sx={{ flexGrow: 1 }}>
        <TextField
          fullWidth
          value={localValue}
          onChange={handleChange}
          placeholder={placeholder || t('search.placeholder', 'Search files and folders...')}
          disabled={loading}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon color="action" />
              </InputAdornment>
            ),
            endAdornment: localValue && (
              <InputAdornment position="end">
                <IconButton
                  size="small"
                  onClick={handleClear}
                  disabled={loading}
                  aria-label={t('common:actions.clear', 'Clear')}
                >
                  <ClearIcon fontSize="small" />
                </IconButton>
              </InputAdornment>
            ),
          }}
          variant="outlined"
          size="medium"
        />
      </Box>
    </Paper>
  );
};
