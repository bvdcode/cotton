import { Switch } from "@mui/material";
import { useTranslation } from "react-i18next";
import { SettingsSection } from "./SettingsSection";
import { useAutoSavedSetting } from "./useAutoSavedSetting";
import type { SaveStatus } from "./useAutoSavedSetting";
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

type BooleanSwitchSettingControlProps = Omit<
  BooleanSwitchSettingProps,
  "toastIdPrefix" | "load" | "save"
> & {
  value: boolean;
  commitValue: (value: boolean) => void;
  status: SaveStatus;
  loadFailed: boolean;
};

export const BooleanSwitchSettingControl = ({
  title,
  titleAction,
  description,
  value,
  commitValue,
  status,
  loadFailed,
  highlight = false,
  highlightKey,
}: BooleanSwitchSettingControlProps) => {
  const disabled = loadFailed || status === "loading" || status === "saving";

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

  const { value, commitValue, status, loadFailed } =
    useAutoSavedSetting<boolean>({
      initial: false,
      load,
      save,
      toastIdPrefix,
      loadErrorMessage: t("settings.errors.loadFailed"),
      saveErrorMessage: t("settings.errors.saveFailed"),
    });

  return (
    <BooleanSwitchSettingControl
      title={title}
      titleAction={titleAction}
      description={description}
      value={value}
      commitValue={commitValue}
      status={status}
      loadFailed={loadFailed}
      highlight={highlight}
      highlightKey={highlightKey}
    />
  );
};
