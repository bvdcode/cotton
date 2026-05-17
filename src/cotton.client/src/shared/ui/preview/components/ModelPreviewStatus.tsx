import { Box, CircularProgress, Typography, type TypographyProps } from "@mui/material";
import * as React from "react";

interface ModelPreviewStatusProps {
  absolute?: boolean;
  color?: TypographyProps["color"];
  loading?: boolean;
  message: string;
}

export const ModelPreviewStatus: React.FC<ModelPreviewStatusProps> = ({
  absolute = false,
  color = "text.secondary",
  loading = false,
  message,
}) => {
  return (
    <Box
      sx={{
        position: absolute ? "absolute" : "static",
        inset: absolute ? 0 : undefined,
        width: absolute ? undefined : "100%",
        height: absolute ? undefined : "100%",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        zIndex: absolute ? 1 : undefined,
        px: 2,
        p: absolute ? undefined : 2,
      }}
    >
      {loading ? (
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            gap: 1,
          }}
        >
          <CircularProgress size={20} />
          <Typography color={color}>{message}</Typography>
        </Box>
      ) : (
        <Typography color={color}>{message}</Typography>
      )}
    </Box>
  );
};