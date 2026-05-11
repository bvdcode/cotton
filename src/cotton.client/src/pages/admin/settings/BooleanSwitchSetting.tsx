import { Switch } from "@mui/material";
import { useTranslation } from "react-i18next";
import { SettingsSection } from "./SettingsSection";
import { useAutoSavedSetting } from "./useAutoSavedSetting";
import type { ReactNode } from "react";

type BooleanSwitchSettingProps = {
  title: ReactNode;
  titleAction?: ReactNode;
  description?: ReactNode;
  toastIdPrefix: string;
  load: () => Promise<boolean>;
  save: (value: boolean) => Promise<void>;
  highlight?: boolean;
  highlightKey?: string;
};

export const BooleanSwitchSetting = ({
  title,
  titleAction,
  description,
  toastIdPrefix,
  load,
  save,
  highlight = false,
  highlightKey,
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
      titleAction={titleAction}
      description={description}
      status={status}
      highlight={highlight}
      highlightKey={highlightKey}
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
