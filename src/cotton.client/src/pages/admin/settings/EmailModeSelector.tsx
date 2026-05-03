import { MenuItem, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { EmailMode } from "../../../shared/api/settingsApi";
import { SettingsSection } from "./SettingsSection";
import type { SaveStatus } from "./useAutoSavedSetting";

const emailModes: EmailMode[] = ["None", "Cloud", "Custom"];

type EmailModeSelectorProps = {
  value: EmailMode;
  onChange: (mode: EmailMode) => void;
  disabled: boolean;
  status: SaveStatus;
};

export const EmailModeSelector = ({
  value,
  onChange,
  disabled,
  status,
}: EmailModeSelectorProps) => {
  const { t } = useTranslation("admin");

  return (
    <SettingsSection
      title={t("notificationsSettings.fields.emailMode")}
      description={t("notificationsSettings.description")}
      status={status}
    >
      <TextField
        select
        value={value}
        onChange={(event) => onChange(event.target.value as EmailMode)}
        disabled={disabled}
        fullWidth
      >
        {emailModes.map((mode) => (
          <MenuItem key={mode} value={mode}>
            {t(`notificationsSettings.emailMode.${mode}`)}
          </MenuItem>
        ))}
      </TextField>
    </SettingsSection>
  );
};
