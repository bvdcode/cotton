import {
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from "@mui/material";
import HelpOutlineIcon from "@mui/icons-material/HelpOutline";
import type { MouseEvent } from "react";
import { useTranslation } from "react-i18next";
import type { StorageSpaceMode } from "@shared/api/settingsApi";
import { storageSpaceOptions } from "../../settings/adminGeneralSettingsModel";
import { AdminSettingStatusIndicator } from "../../settings/AdminSettingStatusIndicator";
import type { SaveStatus } from "../../settings/useAutoSavedSetting";

interface StorageSpaceModeControlProps {
  value: StorageSpaceMode;
  status: SaveStatus;
  disabled: boolean;
  onChange: (
    event: MouseEvent<HTMLElement>,
    nextMode: StorageSpaceMode | null,
  ) => void;
}

export const StorageSpaceModeControl = ({
  value,
  status,
  disabled,
  onChange,
}: StorageSpaceModeControlProps) => {
  const { t } = useTranslation(["admin"]);

  return (
    <Stack spacing={0.75} sx={{ width: "100%" }}>
      <Stack direction="row" spacing={0.75} alignItems="center">
        <Typography variant="body2" color="text.secondary">
          {t("settings.general.fields.storageSpaceMode")}
        </Typography>
        <AdminSettingStatusIndicator status={status} />
        <Tooltip title={t("settings.general.storageSpaceHelp.description")}>
          <HelpOutlineIcon fontSize="small" color="action" />
        </Tooltip>
      </Stack>
      <ToggleButtonGroup
        size="small"
        exclusive
        value={value}
        onChange={onChange}
        disabled={disabled}
        aria-label={t("settings.general.fields.storageSpaceMode")}
        fullWidth
        sx={{
          width: "100%",
          "& .MuiToggleButton-root": {
            flex: 1,
            minWidth: 0,
            whiteSpace: "normal",
            lineHeight: 1.2,
          },
        }}
      >
        {storageSpaceOptions.map((option) => (
          <ToggleButton key={option} value={option}>
            {t(`settings.general.storageSpaceMode.${option}`)}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>
    </Stack>
  );
};
