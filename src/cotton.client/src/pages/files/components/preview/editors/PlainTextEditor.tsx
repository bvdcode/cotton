/**
 * Plain Text Editor Component
 * 
 * Single Responsibility: Handles plain text rendering and editing
 * with preserved whitespace and line breaks
 */

import { Box } from "@mui/material";
import type { IEditorProps } from "./types";

export const PlainTextEditor: React.FC<IEditorProps> = ({
  value,
  onChange,
  isEditing,
}) => {
  if (isEditing) {
    return (
      <Box
        component="textarea"
        value={value || ''}
        onChange={(e) => onChange(e.target.value)}
        sx={{
          width: "100%",
          height: "100%",
          border: "none",
          outline: "none",
          resize: "none",
          p: 2,
          fontFamily: "monospace",
          fontSize: "0.9rem",
          backgroundColor: "background.paper",
          color: "text.primary",
          lineHeight: 1.6,
        }}
      />
    );
  }

  return (
    <Box
      sx={{
        p: 2,
        fontFamily: "monospace",
        fontSize: "0.9rem",
        whiteSpace: "pre-wrap",
        wordBreak: "break-word",
        height: "100%",
        overflow: "auto",
        lineHeight: 1.6,
      }}
    >
      {value}
    </Box>
  );
};
