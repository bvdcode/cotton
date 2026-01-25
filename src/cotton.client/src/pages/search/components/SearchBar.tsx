import { Box, TextField } from "@mui/material";
import type React from "react";

export interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  placeholder?: string;
}

export const SearchBar: React.FC<SearchBarProps> = ({
  value,
  onChange,
  disabled = false,
  placeholder,
}) => {
  return (
    <Box
      sx={{
        display: "flex",
        gap: 1,
        mb: 2,
        alignItems: "center",
      }}
    >
      <TextField
        fullWidth
        size="small"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        disabled={disabled}
      />
    </Box>
  );
};
