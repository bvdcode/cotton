import {
  Alert,
  Button,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { SettingsSection } from "./SettingsSection";
import { computionOptions } from "./adminGeneralSettingsModel";
import {
  getConnectedComputationKey,
  getComputationErrorKey,
  useComputationModeSetting,
} from "./useComputationModeSetting";

export const ComputationModeSetting = ({
  smartSearch = false,
}: {
  smartSearch?: boolean;
}) => {
  const { t } = useTranslation("admin");
  const {
    mode,
    url,
    settings,
    save,
    validation,
    disabled,
    status,
    savedService,
    showingSavedService,
    service,
    selectMode,
    changeUrl,
    submitRemote,
    saveError,
  } = useComputationModeSetting();
  const serviceError = savedService?.error;

  return (
    <SettingsSection
      title={t(
        smartSearch
          ? "smartSearch.wizard.provider"
          : "settings.general.fields.computionMode",
      )}
      status={status}
    >
      <Stack
        spacing={1.5}
        component="form"
        onSubmit={(event) => {
          event.preventDefault();
          submitRemote();
        }}
      >
        <TextField
          select
          value={smartSearch && mode === "Local" ? "" : mode}
          disabled={disabled}
          fullWidth
          onChange={(event) => selectMode(event.target.value)}
          slotProps={{
            select: {
              displayEmpty: smartSearch,
              inputProps: {
                "aria-label": t(
                  smartSearch
                    ? "smartSearch.wizard.provider"
                    : "settings.general.fields.computionMode",
                ),
              },
            },
          }}
        >
          {smartSearch && (
            <MenuItem value="" disabled>
              {t("smartSearch.wizard.chooseService")}
            </MenuItem>
          )}
          {computionOptions
            .filter((option) => !smartSearch || option !== "Local")
            .map((option) => (
              <MenuItem key={option} value={option}>
                {t(
                  smartSearch && option === "Remote"
                    ? "smartSearch.wizard.remoteService"
                    : `settings.general.computionMode.${option}`,
                )}
              </MenuItem>
            ))}
        </TextField>
        {mode === "Remote" && (
          <>
            <Stack
              direction={{ xs: "column", sm: "row" }}
              sx={{
                alignItems: "flex-start",
                gap: 1.5,
              }}
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
                onChange={(event) => changeUrl(event.target.value)}
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
          </>
        )}
        {smartSearch && service.isFetching && !savedService && (
          <Typography role="status" variant="body2">
            {t("smartSearch.wizard.checkingService")}
          </Typography>
        )}
        {!save.isError && savedService?.isReady && savedService.info && (
          <Typography
            variant="body2"
            role="status"
            sx={{
              color: "text.secondary",
            }}
          >
            {t(getConnectedComputationKey(mode), {
              model: savedService.info.modelId,
              dimensions: savedService.dimensions,
              tokens: savedService.info.maxInputTokens,
            })}
          </Typography>
        )}
        {settings.isError && (
          <Alert severity="error">{t("settings.errors.loadFailed")}</Alert>
        )}
        {!save.isError && showingSavedService && service.isError && (
          <Alert severity="warning">
            {t("settings.general.remoteRunner.statusLoadFailed")}
          </Alert>
        )}
        {save.isError && <Alert severity="error">{saveError}</Alert>}
        {!save.isError && serviceError && (
          <Alert severity="warning">
            {t(getComputationErrorKey(mode, serviceError))}
          </Alert>
        )}
      </Stack>
    </SettingsSection>
  );
};
