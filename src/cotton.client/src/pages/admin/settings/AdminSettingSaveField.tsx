import SaveIcon from "@mui/icons-material/Save";
import {
  Box,
  CircularProgress,
  IconButton,
  type SxProps,
  type Theme,
} from "@mui/material";
import { alpha } from "@mui/material/styles";
import type { ReactNode } from "react";

type AdminSettingSaveFieldProps = {
  label: string;
  disabled: boolean;
  saving?: boolean;
  onSave: () => void;
  children: ReactNode;
  sx?: SxProps<Theme>;
};

export const AdminSettingSaveField = ({
  label,
  disabled,
  saving = false,
  onSave,
  children,
  sx,
}: AdminSettingSaveFieldProps) => (
  <Box
    sx={[
      {
        display: "grid",
        gridTemplateColumns: "minmax(0, 1fr) auto",
        alignItems: "start",
        width: "100%",
        "& .AdminSettingSaveField-control .MuiOutlinedInput-root": {
          borderTopRightRadius: 0,
          borderBottomRightRadius: 0,
          bgcolor: (theme) => alpha(theme.palette.common.white, 0.02),
          transition:
            "border-color 120ms ease, background-color 120ms ease, box-shadow 120ms ease",
        },
        "& .MuiOutlinedInput-root": {
          borderTopRightRadius: 0,
          borderBottomRightRadius: 0,
        },
        "& .MuiOutlinedInput-notchedOutline": {
          borderRightWidth: 0,
          borderTopRightRadius: 0,
          borderBottomRightRadius: 0,
        },
        "& .MuiAutocomplete-endAdornment": {
          right: 4,
        },
        "&:hover .MuiOutlinedInput-notchedOutline": {
          borderColor: "text.primary",
        },
        "&:hover .AdminSettingSaveField-buttonRoot": {
          borderColor: "text.primary",
        },
        "&:focus-within .AdminSettingSaveField-control .MuiOutlinedInput-root": {
          bgcolor: (theme) => alpha(theme.palette.common.white, 0.03),
        },
        "&:focus-within .AdminSettingSaveField-buttonRoot": {
          borderColor: "primary.main",
        },
      },
      ...(Array.isArray(sx) ? sx : [sx]),
    ]}
  >
    <Box className="AdminSettingSaveField-control" minWidth={0}>
      {children}
    </Box>
    <Box
      className="AdminSettingSaveField-buttonRoot"
      sx={{
        width: 52,
        height: 56,
        borderTop: 1,
        borderRight: 1,
        borderBottom: 1,
        borderLeft: 0,
        borderColor: "divider",
        bgcolor: (theme) => alpha(theme.palette.common.white, 0.02),
        borderTopRightRadius: (theme) => theme.shape.borderRadius,
        borderBottomRightRadius: (theme) => theme.shape.borderRadius,
        transition:
          "border-color 120ms ease, background-color 120ms ease, box-shadow 120ms ease",
      }}
    >
      <IconButton
        className="AdminSettingSaveField-button"
        aria-label={label}
        color="primary"
        disabled={disabled}
        onClick={onSave}
        sx={{
          width: "100%",
          height: "100%",
          borderRadius: 0,
          bgcolor: "transparent",
          "&:hover": {
            bgcolor: "transparent",
          },
          "&.Mui-disabled": {
            bgcolor: "transparent",
          },
        }}
      >
        {saving ? <CircularProgress size={20} /> : <SaveIcon />}
      </IconButton>
    </Box>
  </Box>
);
