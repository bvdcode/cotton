import { Box, Button, TextField } from "@mui/material";
import { Search } from "@mui/icons-material";
import type React from "react";

export interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  onSubmit: () => void;
  disabled?: boolean;
}

export const SearchBar: React.FC<SearchBarProps> = ({
  value,
  onChange,
  onSubmit,
  disabled = false,
}) => {
  const handleKeyDown: React.KeyboardEventHandler<HTMLInputElement> = (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      onSubmit();
    }
  };

  return (
    <Box
      component="form"
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit();
      }}
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
        onKeyDown={handleKeyDown}
        placeholder="Search in layout"
        disabled={disabled}
      />
      <Button
        variant="contained"
        startIcon={<Search />}
        type="submit"
        disabled={disabled}
      >
        Search
      </Button>
    </Box>
  );
};
