import { Switch } from "@mui/material";
import { useTranslation } from "react-i18next";
import { SettingsSection } from "./SettingsSection";
import { useAutoSavedSetting } from "./useAutoSavedSetting";
import type { ReactNode } from "react";

type BooleanSwitchSettingProps = {
  title: ReactNode;
  description?: ReactNode;
  toastIdPrefix: string;
  load: () => Promise<boolean>;
  save: (value: boolean) => Promise<void>;
};

export const BooleanSwitchSetting = ({
  title,
  description,
  toastIdPrefix,
  load,
  save,
}: BooleanSwitchSettingProps) => {
  const { t } = useTranslation("admin");

  const { value, commitValue, status } = useAutoSavedSetting<boolean>({
    initial: false,
    load,
    save,
    toastIdPrefix,
    errorMessage: t("settings.errors.saveFailed"),
  });

  const disabled = status === "loading" || status === "saving";

  return (
    <SettingsSection
      title={title}
      description={description}
      status={status}
      action={
        <Switch
          checked={value}
          onChange={(event) => commitValue(event.target.checked)}
          disabled={disabled}
        />
      }
    />
  );
};
