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
        "&:focus-within .AdminSettingSaveField-button": {
          borderColor: "primary.main",
          borderWidth: 2,
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
            border: 1,
            borderLeftWidth: 1,
            borderColor: "divider",
            borderRadius: "0 4px 4px 0",
            bgcolor: "background.paper",
            transition:
              "border-color 120ms ease, background-color 120ms ease",
            "&:hover": {
              bgcolor: "action.hover",
              borderColor: "text.primary",
            },
            "&.Mui-disabled": {
              bgcolor: "action.disabledBackground",
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
