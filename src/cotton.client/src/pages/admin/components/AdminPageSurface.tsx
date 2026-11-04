import { Box, Paper } from "@mui/material";
import type { ReactNode } from "react";

export const ADMIN_PAGE_SURFACE_WIDTH = 1200;

interface AdminPageSurfaceProps {
  children: ReactNode;
  fullHeight?: boolean;
}

export const AdminPageSurface = ({
  children,
  fullHeight = false,
}: AdminPageSurfaceProps) => (
  <Box
    sx={{
      width: "100%",
      display: "flex",
      justifyContent: "center",
      height: fullHeight ? "100%" : undefined,
      minHeight: 0,
    }}
  >
    <Paper
      sx={{
        width: `min(100%, ${ADMIN_PAGE_SURFACE_WIDTH}px)`,
        overflow: fullHeight ? "hidden" : "auto",
        scrollbarWidth: "thin",
        height: fullHeight ? "100%" : undefined,
        minHeight: 0,
        display: fullHeight ? "flex" : undefined,
        flexDirection: fullHeight ? "column" : undefined,
      }}
    >
      {children}
    </Paper>
  </Box>
);
