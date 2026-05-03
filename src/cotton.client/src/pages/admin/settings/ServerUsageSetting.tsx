import { Checkbox, FormControlLabel, FormGroup } from "@mui/material";
import { useTranslation } from "react-i18next";
import {
  settingsApi,
  type ServerUsage,
} from "../../../shared/api/settingsApi";
import { SettingsSection } from "./SettingsSection";
import { useAutoSavedSetting } from "./useAutoSavedSetting";
import { isSameArray, usageOptions } from "./adminGeneralSettingsModel";

const loadServerUsage = async (): Promise<ServerUsage[]> => {
  const next = await settingsApi.getServerUsage();
  return next.length > 0 ? next : ["Other"];
};

export const ServerUsageSetting = () => {
  const { t } = useTranslation("admin");

  const { value, commitValue, status } = useAutoSavedSetting<ServerUsage[]>({
    initial: ["Other"],
    load: loadServerUsage,
    save: settingsApi.setServerUsage,
    toastIdPrefix: "admin-general:server-usage",
    errorMessage: t("settings.errors.saveFailed"),
    isEqual: isSameArray,
  });

  const disabled = status === "loading" || status === "saving";

  const toggle = (option: ServerUsage) => {
    const toggled = value.includes(option)
      ? value.filter((item) => item !== option)
      : [...value, option];
    const next = toggled.length > 0 ? toggled : (["Other"] satisfies ServerUsage[]);
    commitValue(next);
  };

  return (
    <SettingsSection
      title={t("settings.general.fields.serverUsage")}
      description={t("settings.general.help.serverUsage")}
      status={status}
    >
      <FormGroup row>
        {usageOptions.map((option) => (
          <FormControlLabel
            key={option}
            control={
              <Checkbox
                checked={value.includes(option)}
                onChange={() => toggle(option)}
                disabled={disabled}
              />
            }
            label={t(`settings.general.serverUsage.${option}`)}
          />
        ))}
      </FormGroup>
    </SettingsSection>
  );
};
