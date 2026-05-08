import { Autocomplete, TextField } from "@mui/material";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { settingsApi } from "../../../shared/api/settingsApi";
import { SettingsSection } from "./SettingsSection";
import { useAutoSavedSetting } from "./useAutoSavedSetting";
import { getSupportedTimeZones } from "./adminGeneralSettingsModel";

export const TimezoneSetting = () => {
  const { t } = useTranslation("admin");

  const timeZoneOptions = useMemo(() => getSupportedTimeZones(), []);

  const { value, commitValue, status } = useAutoSavedSetting<string>({
    initial: "UTC",
    load: async () => (await settingsApi.getTimezone()).trim() || "UTC",
    save: settingsApi.setTimezone,
    toastIdPrefix: "admin-general:timezone",
    errorMessage: t("settings.errors.saveFailed"),
  });

  const options = useMemo(() => {
    if (timeZoneOptions.includes(value) || !value) return timeZoneOptions;
    return [value, ...timeZoneOptions];
  }, [timeZoneOptions, value]);

  const disabled = status === "loading" || status === "saving";

  return (
    <SettingsSection
      title={t("settings.general.fields.timezone")}
      description={t("settings.general.help.timezone")}
      status={status}
    >
      <Autocomplete
        options={options}
        value={value}
        onChange={(_, next) => {
          if (next) commitValue(next);
        }}
        disabled={disabled}
        disableClearable
        autoHighlight
        fullWidth
        renderInput={(params) => <TextField {...params} />}
      />
    </SettingsSection>
  );
};
