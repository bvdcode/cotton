import React from "react";
import { Input } from "@mui/material";

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

const stopDragStart = (event: React.DragEvent): void => {
  event.preventDefault();
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
    <Input
      autoFocus
      fullWidth
      value={value}
      placeholder={placeholder}
      onChange={(event) => onChange(event.target.value)}
      onKeyDown={handleKeyDown}
      onBlur={() => {
        void onConfirm();
      }}
      onClick={stopEventPropagation}
      onMouseDown={stopEventPropagation}
      onDoubleClick={stopEventPropagation}
      onDragStart={stopDragStart}
      sx={{
        fontSize: "inherit",
        fontWeight: "inherit",
        lineHeight: "inherit",
        "& input": { p: 0, height: "auto" },
      }}
    />
  );
};
