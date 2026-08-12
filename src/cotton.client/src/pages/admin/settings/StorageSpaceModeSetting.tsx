import { ToggleButton } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { StorageSpaceMode } from "@shared/api/settingsApi";
import { SettingsSection } from "./SettingsSection";
import { SettingsToggleButtonGroup } from "./SettingsToggleButtonGroup";
import { storageSpaceOptions } from "./adminGeneralSettingsModel";
import type { SaveStatus } from "./useAutoSavedSetting";

interface StorageSpaceModeSettingProps {
  disabled: boolean;
  onChange: (value: StorageSpaceMode | null) => void;
  status: SaveStatus;
  value: StorageSpaceMode;
}

export const StorageSpaceModeSetting = ({
  disabled,
  onChange,
  status,
  value,
}: StorageSpaceModeSettingProps) => {
  const { t } = useTranslation("admin");

  return (
    <SettingsSection
      title={t("settings.general.fields.storageSpaceMode")}
      description={t("settings.general.storageSpaceHelp.description")}
      status={status}
    >
      <SettingsToggleButtonGroup
        value={value}
        onChange={onChange}
        disabled={disabled}
        ariaLabel={t("settings.general.fields.storageSpaceMode")}
      >
        {storageSpaceOptions.map((option) => (
          <ToggleButton key={option} value={option}>
            {t(`settings.general.storageSpaceMode.${option}`)}
          </ToggleButton>
        ))}
      </SettingsToggleButtonGroup>
    </SettingsSection>
  );
};
