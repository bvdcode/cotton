import { IconButton, Tooltip } from "@mui/material";
import { Shield, ShieldOutlined } from "@mui/icons-material";

interface TrustDeviceToggleProps {
  active: boolean;
  onToggle: () => void;
  disabled: boolean;
  tooltip: string;
}

export const TrustDeviceToggle = ({
  active,
  onToggle,
  disabled,
  tooltip,
}: TrustDeviceToggleProps) => (
  <Tooltip title={tooltip}>
    <IconButton
      color={active ? "primary" : "default"}
      onClick={onToggle}
      disabled={disabled}
      sx={{
        border: 1,
        borderColor: active ? "primary.main" : "divider",
      }}
    >
      {active ? <Shield /> : <ShieldOutlined />}
    </IconButton>
  </Tooltip>
);
