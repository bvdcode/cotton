import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import { Box, CircularProgress } from "@mui/material";
import type { SaveStatus } from "./useAutoSavedSetting";

type AdminSettingStatusIndicatorProps = {
  status: SaveStatus;
};

export const AdminSettingStatusIndicator = ({
  status,
}: AdminSettingStatusIndicatorProps) => (
  <Box
    sx={{
      width: 18,
      height: 18,
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      flex: "0 0 auto",
    }}
  >
    {(status === "loading" || status === "saving") && (
      <CircularProgress size={14} thickness={5} />
    )}

    {status === "saved" && (
      <CheckCircleOutlineIcon
        sx={{
          fontSize: 18,
          color: "success.main",
          animation:
            "adminSettingSavedIn 220ms ease-out, adminSettingSavedGlow 1600ms ease-in-out 180ms",
          "@keyframes adminSettingSavedIn": {
            "0%": {
              opacity: 0,
              transform: "scale(0.7) translateY(2px)",
            },
            "70%": {
              opacity: 1,
              transform: "scale(1.12) translateY(0)",
            },
            "100%": {
              opacity: 1,
              transform: "scale(1) translateY(0)",
            },
          },
          "@keyframes adminSettingSavedGlow": {
            "0%, 100%": {
              filter: "drop-shadow(0 0 0 rgba(46, 125, 50, 0))",
            },
            "45%": {
              filter: "drop-shadow(0 0 5px rgba(46, 125, 50, 0.45))",
            },
          },
        }}
      />
    )}

    {status === "error" && (
      <ErrorOutlineIcon sx={{ fontSize: 18, color: "error.main" }} />
    )}
  </Box>
);
