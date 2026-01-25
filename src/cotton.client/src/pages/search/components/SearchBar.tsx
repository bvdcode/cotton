/**
 * Search Bar Component
 * 
 * Single Responsibility: Provides search input interface
 * Open/Closed: Can be extended with filters without modification
 */

import React, { useState, useCallback, useEffect, useRef } from 'react';
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
  const [localValue, setLocalValue] = useState('');
  const debounceTimerRef = useRef<number | null>(null);
  const initializedRef = useRef(false);

  // Initialize only once on mount
  useEffect(() => {
    if (!initializedRef.current) {
      setLocalValue(value);
      initializedRef.current = true;
    }
  }, [value]);

  const handleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const newValue = e.target.value;
    setLocalValue(newValue);

    // Clear previous timer
    if (debounceTimerRef.current) {
      window.clearTimeout(debounceTimerRef.current);
    }

    // Set new timer for debounced search
    debounceTimerRef.current = window.setTimeout(() => {
      onSearch(newValue.trim());
    }, 500); // 500ms debounce
  }, [onSearch]);

  const handleClear = useCallback(() => {
    setLocalValue('');
    if (debounceTimerRef.current) {
      window.clearTimeout(debounceTimerRef.current);
    }
    onClear?.();
  }, [onClear]);

  // Cleanup timer on unmount
  useEffect(() => {
    return () => {
      if (debounceTimerRef.current) {
        window.clearTimeout(debounceTimerRef.current);
      }
    };
  }, []);

  return (
    <Paper
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
          placeholder={placeholder || t('search:placeholder')}
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
                  aria-label={t('common:actions.clear')}
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
