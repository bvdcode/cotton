import { MenuItem, TextField } from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  settingsApi,
  type ComputionMode,
} from "../../../shared/api/settingsApi";
import { SettingsSection } from "./SettingsSection";
import { computionOptions } from "./adminGeneralSettingsModel";

export const ComputationModeSetting = () => {
  const { t } = useTranslation("admin");
  const [mode, setMode] = useState<ComputionMode>("Local");

  useEffect(() => {
    let active = true;
    void settingsApi
      .getComputionMode()
      .then((next) => {
        if (active) setMode(next);
      })
      .catch(() => {
        // Silent: the field is read-only in development; surface load failures via other settings.
      });
    return () => {
      active = false;
    };
  }, []);

  return (
    <SettingsSection
      title={t("settings.general.fields.computionMode")}
      description={t("settings.general.computionMode.inDevelopment")}
    >
      <TextField select value={mode} disabled fullWidth>
        {computionOptions.map((option) => (
          <MenuItem key={option} value={option}>
            {t(`settings.general.computionMode.${option}`)}
          </MenuItem>
        ))}
      </TextField>
    </SettingsSection>
  );
};
