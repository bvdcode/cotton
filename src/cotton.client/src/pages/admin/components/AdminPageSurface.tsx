import { Box, Paper } from "@mui/material";
import type { ReactNode } from "react";

export const ADMIN_PAGE_SURFACE_WIDTH = 1280;

interface AdminPageSurfaceProps {
  children: ReactNode;
}

export const AdminPageSurface = ({ children }: AdminPageSurfaceProps) => (
  <Box sx={{ width: "100%", display: "flex", justifyContent: "center" }}>
    <Paper
      sx={{
        width: `min(100%, ${ADMIN_PAGE_SURFACE_WIDTH}px)`,
        overflow: "hidden",
      }}
    >
      {children}
    </Paper>
  </Box>
);
