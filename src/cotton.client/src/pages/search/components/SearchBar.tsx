import { Box, TextField } from "@mui/material";
import type React from "react";

export interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  placeholder?: string;
  ariaLabel?: string;
}

export const SearchBar: React.FC<SearchBarProps> = ({
  value,
  onChange,
  disabled = false,
  placeholder,
  ariaLabel,
}) => {
  return (
    <Box
      role="search"
      display="flex"
      gap={1}
      my={3}
      alignItems="center"
    >
      <TextField
        fullWidth
        autoFocus
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        disabled={disabled}
        slotProps={{
          input: {
            "aria-label": ariaLabel ?? placeholder,
          },
        }}
      />
    </Box>
  );
};
