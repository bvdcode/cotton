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
    {saving && (
      <Box
        sx={{
          position: "absolute",
          inset: 0,
          zIndex: 2,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          borderRadius: 1,
          pointerEvents: "none",
          bgcolor: (theme) => alpha(theme.palette.background.paper, 0.72),
        }}
      >
        <CircularProgress size={22} />
      </Box>
    )}
  </Box>
);
