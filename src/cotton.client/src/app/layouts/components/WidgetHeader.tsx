import React from "react";
import { Box, IconButton, LinearProgress, Typography } from "@mui/material";
import { Close, ExpandMore, ExpandLess } from "@mui/icons-material";

interface WidgetHeaderProps {
  title: string;
  isCollapsed: boolean;
  hasActive: boolean;
  progressPercent: number;
  onToggleCollapse: () => void;
  onClose: () => void;
  aria: {
    expand: string;
    collapse: string;
    close: string;
  };
}

export const WidgetHeader: React.FC<WidgetHeaderProps> = ({
  title,
  isCollapsed,
  hasActive,
  progressPercent,
  onToggleCollapse,
  onClose,
  aria,
}) => {
  return (
    <Box
      sx={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        mb: isCollapsed ? 0 : 1,
        position: "relative",
        zIndex: 1,
        borderRadius: isCollapsed ? 2 : 0,
        overflow: "hidden",
        p: 1.5,
        transition: "border-radius 0.5s ease-in-out",
      }}
    >
      {hasActive && (
        <LinearProgress
          variant="determinate"
          value={progressPercent}
          color="success"
          aria-label={title}
          sx={{
            position: "absolute",
            inset: 0,
            height: "100%",
            bgcolor: "transparent",
            opacity: 0.15,
            zIndex: 0,
            pointerEvents: "none",
            "& .MuiLinearProgress-bar": {
              transition: "transform 0.3s ease-out",
            },
          }}
        />
      )}
      <Typography
        variant="subtitle1"
        sx={{
          fontWeight: 600,
          position: "relative",
          zIndex: 1,
          color: "text.primary",
        }}
      >
        {title}
      </Typography>
      <Box
        sx={{
          display: "flex",
          gap: 0.5,
          position: "relative",
          zIndex: 1,
        }}
      >
        <IconButton
          size="small"
          onClick={onToggleCollapse}
          aria-label={isCollapsed ? aria.expand : aria.collapse}
        >
          {isCollapsed ? (
            <ExpandLess fontSize="small" />
          ) : (
            <ExpandMore fontSize="small" />
          )}
        </IconButton>
        {!hasActive && (
          <IconButton size="small" onClick={onClose} aria-label={aria.close}>
            <Close fontSize="small" />
          </IconButton>
        )}
      </Box>
    </Box>
  );
};
