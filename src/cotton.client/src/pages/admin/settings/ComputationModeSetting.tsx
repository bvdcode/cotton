import {
  Alert,
  Button,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  settingsApi,
  type ComputionMode,
} from "../../../shared/api/settingsApi";
import {
  getComputationError,
  type ComputationStatus,
} from "../../../shared/api/computation";
import { SettingsSection } from "./SettingsSection";
import {
  computionOptions,
  validateRemoteComputationRunnerUrl,
} from "./adminGeneralSettingsModel";

type ComputationSettings = {
  mode: ComputionMode;
  url: string;
  service: ComputationStatus | null;
};

const queryKey = ["admin", "computation-settings"] as const;

const loadSettings = async (): Promise<ComputationSettings> => {
  const [mode, url] = await Promise.all([
    settingsApi.getComputionMode(),
    settingsApi.getRemoteComputationRunnerUrl(),
  ]);
  const service =
    mode === "Remote" ? await settingsApi.getComputationStatus() : null;
  return { mode, url: url.trim(), service };
};

const saveSettings = async (value: {
  mode: ComputionMode;
  url: string;
}): Promise<ComputationSettings> => {
  switch (value.mode) {
    case "Remote":
      return {
        ...value,
        service: await settingsApi.setRemoteComputationRunnerUrl(value.url),
      };
    case "Local":
    case "Cloud":
      await settingsApi.setComputionMode(value.mode);
      return { ...value, service: null };
  }
};

const isComputionMode = (value: string): value is ComputionMode =>
  computionOptions.some((option) => option === value);

export const ComputationModeSetting = () => {
  const { t } = useTranslation("admin");
  const queryClient = useQueryClient();
  const settings = useQuery({
    queryKey,
    queryFn: loadSettings,
    retry: false,
    refetchOnWindowFocus: false,
  });
  const [draftMode, setDraftMode] = useState<ComputionMode | null>(null);
  const [draftUrl, setDraftUrl] = useState<string | null>(null);
  const mode = draftMode ?? settings.data?.mode ?? "Local";
  const url = draftUrl ?? settings.data?.url ?? "";
  const save = useMutation({
    mutationFn: saveSettings,
    onError: (_error, value) => {
      if (value.mode !== "Remote") {
        setDraftMode(null);
        setDraftUrl(null);
      }
    },
    onSuccess: (value) => {
      queryClient.setQueryData(queryKey, value);
      setDraftMode(null);
      setDraftUrl(null);
    },
  });
  const validation = validateRemoteComputationRunnerUrl(
    url,
    mode === "Remote",
    t("settings.general.validation.required"),
    t("settings.general.validation.remoteComputationRunnerUrlInvalid"),
  );
  const busy = settings.isPending || save.isPending;
  const disabled = busy || settings.isError;
  const failure = getComputationError(save.error);
  const savedService =
    mode === settings.data?.mode && validation.normalized === settings.data?.url
      ? settings.data?.service
      : null;
  const serviceError = savedService?.error;

  return (
    <SettingsSection
      title={t("settings.general.fields.computionMode")}
      status={
        settings.isPending ? "loading" : save.isPending ? "saving" : "idle"
      }
    >
      <Stack
        spacing={1.5}
        component="form"
        onSubmit={(event) => {
          event.preventDefault();
          if (
            !disabled &&
            mode === "Remote" &&
            validation.normalized !== null
          ) {
            save.mutate({ mode, url: validation.normalized });
          }
        }}
      >
        <TextField
          select
          value={mode}
          disabled={disabled}
          fullWidth
          SelectProps={{
            inputProps: {
              "aria-label": t("settings.general.fields.computionMode"),
            },
          }}
          onChange={(event) => {
            const next = event.target.value;
            if (!isComputionMode(next)) {
              return;
            }
            save.reset();
            setDraftMode(next);
            if (next !== "Remote") {
              save.mutate({ mode: next, url: settings.data?.url ?? "" });
            }
          }}
        >
          {computionOptions.map((option) => (
            <MenuItem key={option} value={option}>
              {t(`settings.general.computionMode.${option}`)}
            </MenuItem>
          ))}
        </TextField>
        {mode === "Remote" && (
          <>
            <Stack
              direction={{ xs: "column", sm: "row" }}
              alignItems="flex-start"
              gap={1.5}
            >
              <TextField
                label={t("settings.general.fields.remoteComputationRunnerUrl")}
                slotProps={{
                  inputLabel: {
                    sx: {
                      "&.Mui-focused:not(.Mui-error)": {
                        color: "text.primary",
                      },
                    },
                  },
                }}
                value={url}
                disabled={disabled}
                fullWidth
                onChange={(event) => {
                  setDraftUrl(event.target.value);
                  save.reset();
                }}
                error={Boolean(validation.error)}
                helperText={validation.error}
              />
              <Button
                type="submit"
                variant="contained"
                loading={save.isPending}
                disabled={disabled || validation.normalized === null}
                sx={{ flexShrink: 0, minHeight: 56 }}
              >
                {t("settings.general.remoteRunner.validateAndSave")}
              </Button>
            </Stack>
            {!save.isError && savedService?.isReady && savedService.info && (
              <Typography variant="body2" color="text.secondary" role="status">
                {t("settings.general.remoteRunner.connected", {
                  model: savedService.info.modelId,
                  dimensions: savedService.dimensions,
                  tokens: savedService.info.maxInputTokens,
                })}
              </Typography>
            )}
          </>
        )}
        {settings.isError && (
          <Alert severity="error">{t("settings.errors.loadFailed")}</Alert>
        )}
        {save.isError && (
          <Alert severity="error">
            {failure
              ? t(`settings.general.remoteRunner.errors.${failure}`)
              : t("settings.errors.saveFailed")}
          </Alert>
        )}
        {!save.isError && serviceError && (
          <Alert severity="warning">
            {t(`settings.general.remoteRunner.errors.${serviceError}`)}
          </Alert>
        )}
      </Stack>
    </SettingsSection>
  );
};
