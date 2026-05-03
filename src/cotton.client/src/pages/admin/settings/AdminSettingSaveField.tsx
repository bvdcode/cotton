import SaveIcon from "@mui/icons-material/Save";
import { HelpOutline } from "@mui/icons-material";
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
  help?: string;
  sx?: SxProps<Theme>;
};

export const AdminSettingSaveField = ({
  label,
  disabled,
  saving = false,
  onSave,
  children,
  help,
  sx,
}: AdminSettingSaveFieldProps) => (
  <Box
    sx={[
      { display: "flex", alignItems: "flex-start", gap: 1, width: "100%" },
      ...(Array.isArray(sx) ? sx : [sx]),
    ]}
  >
    <Box flex={1} minWidth={0}>
      {children}
    </Box>
    {help && (
      <Tooltip title={help}>
        <Box
          component="span"
          sx={{
            mt: 1.5,
            color: "text.secondary",
            display: "flex",
            cursor: "help",
          }}
        >
          <HelpOutline fontSize="small" />
        </Box>
      </Tooltip>
    )}
    <Tooltip title={label}>
      <span>
        <IconButton
          aria-label={label}
          color="primary"
          disabled={disabled}
          onClick={onSave}
          sx={{ mt: 1 }}
        >
          {saving ? <CircularProgress size={20} /> : <SaveIcon />}
        </IconButton>
      </span>
    </Tooltip>
  </Box>
);
