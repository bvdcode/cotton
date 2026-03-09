import * as React from "react";
import { Box, Typography } from "@mui/material";

type DraggingOverlayProps = {
  open: boolean;
  onDragEnter: React.DragEventHandler<HTMLDivElement>;
  onDragOver: React.DragEventHandler<HTMLDivElement>;
  onDragLeave: React.DragEventHandler<HTMLDivElement>;
  onDrop: React.DragEventHandler<HTMLDivElement>;
  label: string;
};

export const DraggingOverlay: React.FC<DraggingOverlayProps> = ({
  open,
  onDragEnter,
  onDragOver,
  onDragLeave,
  onDrop,
  label,
}) => {
  if (!open) return null;

  return (
    <Box
      onDragEnter={onDragEnter}
      onDragOver={onDragOver}
      onDragLeave={onDragLeave}
      onDrop={onDrop}
      sx={{
        position: "fixed",
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        bgcolor: "primary.main",
        opacity: 0.15,
        border: "4px dashed",
        borderColor: "primary.main",
        zIndex: 9999,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      <Typography
        variant="h3"
        sx={{
          color: "primary.main",
          fontWeight: "bold",
          pointerEvents: "none",
        }}
      >
        {label}
      </Typography>
    </Box>
  );
};
