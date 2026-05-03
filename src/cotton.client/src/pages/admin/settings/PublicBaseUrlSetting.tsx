import { TextField } from "@mui/material";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { settingsApi } from "../../../shared/api/settingsApi";
import { SettingsSection } from "./SettingsSection";
import { useAutoSavedSetting } from "./useAutoSavedSetting";
import {
  normalizeStoredPublicBaseUrl,
  validatePublicBaseUrl,
  type GeneralSettingsValidationMessages,
} from "./adminGeneralSettingsModel";

export const PublicBaseUrlSetting = () => {
  const { t } = useTranslation("admin");

  const messages = useMemo<GeneralSettingsValidationMessages>(
    () => ({
      required: t("settings.general.validation.required"),
      publicBaseUrlInvalid: t(
        "settings.general.validation.publicBaseUrlInvalid",
      ),
      timezoneInvalid: t("settings.general.validation.timezoneInvalid"),
      customGeoIpLookupUrlInvalid: t(
        "settings.general.validation.customGeoIpLookupUrlInvalid",
      ),
    }),
    [t],
  );

  const { value, setValue, commitValue, status } = useAutoSavedSetting<string>({
    initial: "",
    load: async () =>
      normalizeStoredPublicBaseUrl(await settingsApi.getPublicBaseUrl()),
    save: settingsApi.setPublicBaseUrl,
    toastIdPrefix: "admin-general:public-base-url",
    errorMessage: t("settings.errors.saveFailed"),
  });

  const validation = useMemo(
    () => validatePublicBaseUrl(value, messages),
    [value, messages],
  );

  const disabled = status === "loading" || status === "saving";

  const handleBlur = () => {
    if (validation.error || validation.normalized === null) return;
    commitValue(validation.normalized);
  };

  const handleKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter") {
      event.preventDefault();
      handleBlur();
    }
  };

  return (
    <SettingsSection
      title={t("settings.general.fields.publicBaseUrl")}
      description={t("settings.general.help.publicBaseUrl")}
      status={status}
    >
      <TextField
        value={value}
        onChange={(event) => setValue(event.target.value)}
        onBlur={handleBlur}
        onKeyDown={handleKeyDown}
        disabled={disabled}
        error={Boolean(validation.error)}
        helperText={validation.error ?? " "}
        fullWidth
      />
    </SettingsSection>
  );
};
