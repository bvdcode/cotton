import SaveIcon from "@mui/icons-material/Save";
import {
  Box,
  CircularProgress,
  IconButton,
  Tooltip,
  type SxProps,
  type Theme,
} from "@mui/material";
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
        gridTemplateColumns: "minmax(0, 1fr) 48px",
        alignItems: "start",
        width: "100%",
        "& .MuiOutlinedInput-root": {
          borderTopRightRadius: 0,
          borderBottomRightRadius: 0,
        },
        "& .MuiOutlinedInput-notchedOutline": {
          borderRightWidth: 0,
          borderTopRightRadius: 0,
          borderBottomRightRadius: 0,
        },
        "&:hover .MuiOutlinedInput-notchedOutline": {
          borderColor: "text.primary",
        },
        "&:hover .AdminSettingSaveField-button": {
          borderColor: "text.primary",
        },
        "&:focus-within .AdminSettingSaveField-button": {
          borderColor: "primary.main",
          borderTopWidth: 2,
          borderRightWidth: 2,
          borderBottomWidth: 2,
          borderLeftWidth: 0,
        },
      },
      ...(Array.isArray(sx) ? sx : [sx]),
    ]}
  >
    <Box minWidth={0}>{children}</Box>
    <Tooltip title={label}>
      <span>
        <IconButton
          className="AdminSettingSaveField-button"
          aria-label={label}
          color="primary"
          disabled={disabled}
          onClick={onSave}
          sx={{
            width: 48,
            height: 56,
            borderTop: 1,
            borderRight: 1,
            borderBottom: 1,
            borderLeft: 0,
            borderColor: "divider",
            borderRadius: (theme) =>
              `0 ${theme.shape.borderRadius}px ${theme.shape.borderRadius}px 0`,
            bgcolor: "transparent",
            transition:
              "border-color 120ms ease, background-color 120ms ease",
            "&:hover": {
              bgcolor: "transparent",
            },
            "&.Mui-disabled": {
              bgcolor: "transparent",
              borderColor: "divider",
            },
          }}
        >
          {saving ? <CircularProgress size={20} /> : <SaveIcon />}
        </IconButton>
      </span>
    </Tooltip>
  </Box>
);
