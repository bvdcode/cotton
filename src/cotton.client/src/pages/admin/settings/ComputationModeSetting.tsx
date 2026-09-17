import { MenuItem, Stack, TextField } from "@mui/material";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
  settingsApi,
  type ComputionMode,
} from "../../../shared/api/settingsApi";
import { SettingsSection } from "./SettingsSection";
import {
  computionOptions,
  validateRemoteComputationRunnerUrl,
} from "./adminGeneralSettingsModel";
import { useAutoSavedSetting } from "./useAutoSavedSetting";

type ComputationSettings = {
  mode: ComputionMode;
  remoteRunnerUrl: string;
};

const loadComputationSettings = async (): Promise<ComputationSettings> => {
  const [mode, remoteRunnerUrl] = await Promise.all([
    settingsApi.getComputionMode(),
    settingsApi.getRemoteComputationRunnerUrl(),
  ]);
  return { mode, remoteRunnerUrl: remoteRunnerUrl.trim() };
};

const saveComputationSettings = async (
  settings: ComputationSettings,
): Promise<void> => {
  if (settings.mode === "Remote") {
    await settingsApi.setRemoteComputationRunnerUrl(settings.remoteRunnerUrl);
  }
  await settingsApi.setComputionMode(settings.mode);
};

const isSameComputationSettings = (
  left: ComputationSettings,
  right: ComputationSettings,
): boolean =>
  left.mode === right.mode && left.remoteRunnerUrl === right.remoteRunnerUrl;

const isComputionMode = (value: string): value is ComputionMode =>
  computionOptions.some((option) => option === value);

export const ComputationModeSetting = () => {
  const { t } = useTranslation("admin");
  const requiredMessage = t("settings.general.validation.required");
  const invalidUrlMessage = t(
    "settings.general.validation.remoteComputationRunnerUrlInvalid",
  );
  const { value, savedValue, setValue, commitValue, status, loadFailed } =
    useAutoSavedSetting<ComputationSettings>({
      initial: { mode: "Local", remoteRunnerUrl: "" },
      load: loadComputationSettings,
      save: saveComputationSettings,
      toastIdPrefix: "admin-general:computation",
      loadErrorMessage: t("settings.errors.loadFailed"),
      saveErrorMessage: t("settings.errors.saveFailed"),
      isEqual: isSameComputationSettings,
    });

  const urlValidation = useMemo(
    () =>
      validateRemoteComputationRunnerUrl(
        value.remoteRunnerUrl,
        value.mode === "Remote",
        requiredMessage,
        invalidUrlMessage,
      ),
    [invalidUrlMessage, requiredMessage, value.mode, value.remoteRunnerUrl],
  );
  const disabled = loadFailed || status === "loading" || status === "saving";

  const handleModeChange = (mode: ComputionMode) => {
    const next = {
      ...value,
      mode,
      remoteRunnerUrl:
        mode === "Remote" ? value.remoteRunnerUrl : savedValue.remoteRunnerUrl,
    };
    if (mode === "Remote") {
      const remoteValidation = validateRemoteComputationRunnerUrl(
        value.remoteRunnerUrl,
        true,
        requiredMessage,
        invalidUrlMessage,
      );
      if (remoteValidation.normalized === null) {
        setValue(next);
        return;
      }
    }
    commitValue(next);
  };

  const commitUrl = () => {
    if (urlValidation.normalized === null) {
      return;
    }
    commitValue({ ...value, remoteRunnerUrl: urlValidation.normalized });
  };

  const handleUrlKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter") {
      event.preventDefault();
      commitUrl();
    }
  };

  return (
    <SettingsSection
      title={t("settings.general.fields.computionMode")}
      status={status}
    >
      <Stack spacing={2}>
        <TextField
          select
          value={value.mode}
          onChange={(event) => {
            if (isComputionMode(event.target.value)) {
              handleModeChange(event.target.value);
            }
          }}
          disabled={disabled}
          fullWidth
          SelectProps={{
            inputProps: {
              "aria-label": t("settings.general.fields.computionMode"),
            },
          }}
        >
          {computionOptions.map((option) => (
            <MenuItem key={option} value={option}>
              {t(`settings.general.computionMode.${option}`)}
            </MenuItem>
          ))}
        </TextField>
        {value.mode === "Remote" && (
          <TextField
            label={t("settings.general.fields.remoteComputationRunnerUrl")}
            value={value.remoteRunnerUrl}
            onChange={(event) =>
              setValue({ ...value, remoteRunnerUrl: event.target.value })
            }
            onBlur={commitUrl}
            onKeyDown={handleUrlKeyDown}
            disabled={disabled}
            error={Boolean(urlValidation.error)}
            helperText={urlValidation.error ?? " "}
            fullWidth
          />
        )}
      </Stack>
    </SettingsSection>
  );
};
