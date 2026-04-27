import React from "react";
import { Box, InputBase } from "@mui/material";

interface InlineRenameFieldProps {
  value: string;
  onChange: (value: string) => void;
  onConfirm: () => void | Promise<void>;
  onCancel: () => void;
  placeholder?: string;
}

const stopEventPropagation = (event: React.SyntheticEvent): void => {
  event.stopPropagation();
};

export const InlineRenameField: React.FC<InlineRenameFieldProps> = ({
  value,
  onChange,
  onConfirm,
  onCancel,
  placeholder,
}) => {
  const handleKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    event.stopPropagation();

    if (event.key === "Enter") {
      event.preventDefault();
      void onConfirm();
      return;
    }

    if (event.key === "Escape") {
      event.preventDefault();
      onCancel();
    }
  };

  return (
    <Box
      sx={{
        display: "flex",
        alignItems: "center",
        width: "100%",
        minWidth: 0,
        minHeight: 28,
        px: 1,
        py: 0.25,
        borderRadius: 0.75,
        bgcolor: "action.hover",
        border: "1px solid",
        borderColor: "divider",
        transition: "border-color 0.2s ease, background-color 0.2s ease",
        "&:focus-within": {
          borderColor: "primary.main",
          bgcolor: "background.paper",
        },
      }}
      onClick={stopEventPropagation}
      onMouseDown={stopEventPropagation}
      onDoubleClick={stopEventPropagation}
    >
      <InputBase
        autoFocus
        fullWidth
        value={value}
        placeholder={placeholder}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={handleKeyDown}
        onBlur={() => {
          void onConfirm();
        }}
        sx={{
          width: "100%",
          minWidth: 0,
          fontSize: { xs: "0.8rem", md: "0.875rem" },
          lineHeight: 1.43,
          color: "text.primary",
          "& input": {
            p: 0,
            minWidth: 0,
          },
        }}
      />
    </Box>
  );
};