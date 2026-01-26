import React from "react";
import { Box, IconButton, Typography } from "@mui/material";
import { Close, ExpandMore, ExpandLess } from "@mui/icons-material";

interface WidgetHeaderProps {
  title: string;
  isCollapsed: boolean;
  hasActive: boolean;
  progressPercent: number;
  onToggleCollapse: () => void;
  onClose: () => void;
}

export const WidgetHeader: React.FC<WidgetHeaderProps> = ({
  title,
  isCollapsed,
  hasActive,
  progressPercent,
  onToggleCollapse,
  onClose,
}) => {
  return (
    <Box
      display="flex"
      alignItems="center"
      justifyContent="space-between"
      mb={isCollapsed ? 0 : 1}
      sx={{
        position: "relative",
        zIndex: 1,
        borderRadius: isCollapsed ? 2 : 0,
        overflow: "hidden",
        p: 1.5,
        transition: "border-radius 0.5s ease-in-out",
      }}
    >
      {hasActive && (
        <Box
          sx={{
            position: "absolute",
            top: 0,
            left: 0,
            bottom: 0,
            padding: 3,
            width: `${progressPercent}%`,
            bgcolor: "success.main",
            opacity: 0.15,
            transition: "width 0.3s ease-out",
            zIndex: 0,
            pointerEvents: "none",
          }}
        />
      )}
      <Typography
        variant="subtitle1"
        fontWeight={600}
        sx={{
          position: "relative",
          zIndex: 1,
          color: "text.primary",
        }}
      >
        {title}
      </Typography>
      <Box display="flex" gap={0.5} sx={{ position: "relative", zIndex: 1 }}>
        <IconButton
          size="small"
          onClick={onToggleCollapse}
          aria-label={isCollapsed ? "Expand" : "Collapse"}
        >
          {isCollapsed ? (
            <ExpandLess fontSize="small" />
          ) : (
            <ExpandMore fontSize="small" />
          )}
        </IconButton>
        <IconButton size="small" onClick={onClose} aria-label="Close">
          <Close fontSize="small" />
        </IconButton>
      </Box>
    </Box>
  );
};
