import { Box, InputAdornment, TextField } from "@mui/material";
import React, { useEffect, useRef } from "react";
import { DeepSearchButton } from "../../../features/search/components/DeepSearchButton";

export interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  placeholder?: string;
  ariaLabel?: string;
  deep: boolean;
  onToggleDeep: () => void;
}

export const SearchBar: React.FC<SearchBarProps> = ({
  value,
  onChange,
  disabled = false,
  placeholder,
  ariaLabel,
  deep,
  onToggleDeep,
}) => {
  const inputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    if (disabled) {
      return;
    }

    const rafId = window.requestAnimationFrame(() => {
      inputRef.current?.focus();
    });

    return () => window.cancelAnimationFrame(rafId);
  }, [disabled]);

  return (
    <Box role="search" display="flex" gap={1} my={3} alignItems="center">
      <TextField
        fullWidth
        autoFocus
        inputRef={inputRef}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        disabled={disabled}
        slotProps={{
          input: {
            "aria-label": ariaLabel ?? placeholder,
            endAdornment: (
              <InputAdornment position="end">
                <DeepSearchButton
                  enabled={deep}
                  disabled={disabled}
                  onClick={onToggleDeep}
                />
              </InputAdornment>
            ),
          },
        }}
      />
    </Box>
  );
};
