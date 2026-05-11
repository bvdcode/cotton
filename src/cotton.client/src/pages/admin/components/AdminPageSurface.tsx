import { Box, Paper } from "@mui/material";
import type { ReactNode } from "react";

export const ADMIN_PAGE_SURFACE_WIDTHS = {
  default: 880,
  wide: 1280,
} as const;

export type AdminPageSurfaceWidth = keyof typeof ADMIN_PAGE_SURFACE_WIDTHS;

interface AdminPageSurfaceProps {
  children: ReactNode;
  width?: AdminPageSurfaceWidth;
}

export const AdminPageSurface = ({
  children,
  width = "default",
}: AdminPageSurfaceProps) => (
  <Box sx={{ width: "100%", display: "flex", justifyContent: "center" }}>
    <Paper
      sx={{
        width: `min(100%, ${ADMIN_PAGE_SURFACE_WIDTHS[width]}px)`,
        overflow: "hidden",
      }}
    >
      {children}
    </Paper>
  </Box>
);
