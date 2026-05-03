import SaveIcon from "@mui/icons-material/Save";
import { IconButton, Tooltip, type SxProps, type Theme } from "@mui/material";

type AdminSettingSaveIconButtonProps = {
  label: string;
  disabled: boolean;
  onClick: () => void;
  sx?: SxProps<Theme>;
};

export const AdminSettingSaveIconButton = ({
  label,
  disabled,
  onClick,
  sx,
}: AdminSettingSaveIconButtonProps) => (
  <Tooltip title={label}>
    <span>
      <IconButton
        aria-label={label}
        color="primary"
        disabled={disabled}
        onClick={onClick}
        sx={sx}
      >
        <SaveIcon />
      </IconButton>
    </span>
  </Tooltip>
);
