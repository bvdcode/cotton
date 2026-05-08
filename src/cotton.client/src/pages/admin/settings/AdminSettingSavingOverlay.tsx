import { Box, CircularProgress } from "@mui/material";
import { alpha } from "@mui/material/styles";
import type { ReactNode } from "react";

type AdminSettingSavingOverlayProps = {
  saving: boolean;
  children: ReactNode;
};

export const AdminSettingSavingOverlay = ({
  saving,
  children,
}: AdminSettingSavingOverlayProps) => (
  <Box sx={{ position: "relative" }}>
    {children}
    <Box
      sx={{
        position: "absolute",
        inset: 0,
        zIndex: 2,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        borderRadius: 1,
        pointerEvents: saving ? "auto" : "none",
        bgcolor: (theme) => alpha(theme.palette.background.paper, 0.72),
        opacity: saving ? 1 : 0,
        transition: "opacity 150ms ease",
        transitionDelay: saving ? "120ms" : "0ms",
      }}
    >
      <CircularProgress size={22} />
    </Box>
  </Box>
);
