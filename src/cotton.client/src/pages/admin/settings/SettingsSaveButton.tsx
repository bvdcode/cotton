import SaveIcon from "@mui/icons-material/Save";
import { Button, CircularProgress } from "@mui/material";

interface SettingsSaveButtonProps {
  changed: boolean;
  disabled: boolean;
  label: string;
  onSave: () => void;
  saving: boolean;
}

export const SettingsSaveButton = ({
  changed,
  disabled,
  label,
  onSave,
  saving,
}: SettingsSaveButtonProps) => (
  <Button
    variant="contained"
    onClick={onSave}
    disabled={disabled || !changed}
    startIcon={
      saving ? <CircularProgress size={16} color="inherit" /> : <SaveIcon />
    }
    sx={{ minWidth: { xs: "100%", sm: 120 } }}
  >
    {label}
  </Button>
);
