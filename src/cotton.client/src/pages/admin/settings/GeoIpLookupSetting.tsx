import {
  Button,
  CircularProgress,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "@shared/ui/notifications";
import {
  settingsApi,
  type GeoIpLookupMode,
} from "../../../shared/api/settingsApi";
import { showApiErrorToast } from "../../../shared/api/httpClient";
import { SettingsSection } from "./SettingsSection";
import {
  geoIpOptions,
  validateCustomGeoIpLookupUrl,
  type GeneralSettingsValidationMessages,
} from "./adminGeneralSettingsModel";
import type { SaveStatus } from "./useAutoSavedSetting";
import { SAVED_STATUS_VISIBLE_MS } from "./adminSettingSaveStatus";

type LoadedState = {
  mode: GeoIpLookupMode;
  url: string;
  telemetry: boolean;
};

const loadGeoIpState = async (): Promise<LoadedState> => {
  const [mode, url, telemetry] = await Promise.all([
    settingsApi.getGeoIpLookupMode(),
    settingsApi.getCustomGeoIpLookupUrl(),
    settingsApi.getTelemetry(),
  ]);
  return { mode, url: url.trim(), telemetry };
};

export const GeoIpLookupSetting = () => {
  const { t } = useTranslation("admin");

  const [mode, setMode] = useState<GeoIpLookupMode>("Disabled");
  const [savedMode, setSavedMode] = useState<GeoIpLookupMode>("Disabled");
  const [url, setUrl] = useState("");
  const [savedUrl, setSavedUrl] = useState("");
  const [telemetry, setTelemetry] = useState(false);
  const [status, setStatus] = useState<SaveStatus>("loading");
  const [urlTouched, setUrlTouched] = useState(false);
  const [testing, setTesting] = useState(false);

  const flashTimerRef = useRef<number | null>(null);

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

  useEffect(() => {
    let active = true;
    loadGeoIpState()
      .then((next) => {
        if (!active) return;
        setMode(next.mode);
        setSavedMode(next.mode);
        setUrl(next.url);
        setSavedUrl(next.url);
        setTelemetry(next.telemetry);
        setStatus("idle");
      })
      .catch(() => {
        if (!active) return;
        setStatus("idle");
        toast.error(t("settings.errors.loadFailed"), {
          toastId: "admin-general:geoip:load-error",
        });
      });
    return () => {
      active = false;
    };
  }, [t]);

  useEffect(
    () => () => {
      if (flashTimerRef.current !== null) {
        window.clearTimeout(flashTimerRef.current);
      }
    },
    [],
  );

  const flashSaved = useCallback(() => {
    if (flashTimerRef.current !== null) {
      window.clearTimeout(flashTimerRef.current);
    }
    setStatus("saved");
    flashTimerRef.current = window.setTimeout(() => {
      setStatus((current) => (current === "saved" ? "idle" : current));
      flashTimerRef.current = null;
    }, SAVED_STATUS_VISIBLE_MS);
  }, []);

  const reportError = useCallback(
    (error: unknown) => {
      setStatus("error");
      showApiErrorToast(
        error,
        t("settings.errors.saveFailed"),
        "admin-general:geoip:save-error",
      );
    },
    [t],
  );

  const handleModeChange = (next: GeoIpLookupMode) => {
    if (next === mode) return;
    setMode(next);
    setUrlTouched(false);

    if (next === "CustomHttp") {
      // Mode is committed only after a valid URL is saved.
      return;
    }

    setStatus("saving");
    settingsApi
      .setGeoIpLookupMode(next)
      .then(() => {
        setSavedMode(next);
        flashSaved();
      })
      .catch((error) => {
        setMode(savedMode);
        reportError(error);
      });
  };

  const urlValidation = useMemo(
    () =>
      validateCustomGeoIpLookupUrl(url, mode === "CustomHttp", messages),
    [url, mode, messages],
  );

  const urlError = urlTouched ? urlValidation.error : null;

  const commitUrl = useCallback(async (): Promise<boolean> => {
    setUrlTouched(true);
    const next = urlValidation.normalized;
    if (urlValidation.error || next === null) return false;
    if (next === savedUrl && savedMode === "CustomHttp") return true;

    setUrl(next);
    setStatus("saving");

    try {
      await settingsApi.setCustomGeoIpLookupUrl(next);
      if (savedMode !== "CustomHttp") {
        await settingsApi.setGeoIpLookupMode("CustomHttp");
      }

      setSavedUrl(next);
      setSavedMode("CustomHttp");
      flashSaved();
      return true;
    } catch (error) {
      setUrl(savedUrl);
      setMode(savedMode);
      reportError(error);
      return false;
    }
  }, [
    flashSaved,
    reportError,
    savedMode,
    savedUrl,
    urlValidation.error,
    urlValidation.normalized,
  ]);

  const handleTestProvider = useCallback(async () => {
    if (testing) {
      return;
    }

    const isCommitted = await commitUrl();
    if (!isCommitted) {
      return;
    }

    setTesting(true);
    try {
      await settingsApi.testCustomGeoIpLookupUrl();
      toast.success(
        t("settings.general.state.geoIpTestPassed"),
        {
          toastId: "admin-general:geoip:test-success",
        },
      );
    } catch (error) {
      reportError(error);
    } finally {
      setTesting(false);
    }
  }, [commitUrl, reportError, t, testing]);

  const handleUrlKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter") {
      event.preventDefault();
      void commitUrl();
    }
  };

  const disabled = status === "loading" || status === "saving" || testing;

  return (
    <SettingsSection
      title={t("settings.general.fields.geoIpLookupMode")}
      description={t(`settings.general.geoIpLookupModeDescription.${mode}`)}
      status={status}
      action={
        mode === "CustomHttp" ? (
          <Button
            variant="outlined"
            size="small"
            onClick={() => void handleTestProvider()}
            disabled={disabled}
            startIcon={
              testing ? <CircularProgress size={16} color="inherit" /> : null
            }
          >
            {t("settings.general.actions.testGeoIpProvider")}
          </Button>
        ) : undefined
      }
    >
      <Stack spacing={2}>
        <TextField
          select
          value={mode}
          onChange={(event) =>
            handleModeChange(event.target.value as GeoIpLookupMode)
          }
          disabled={disabled}
          fullWidth
          SelectProps={{
            renderValue: (selected) =>
              t(`settings.general.geoIpLookupMode.${selected as GeoIpLookupMode}`),
          }}
        >
          {geoIpOptions.map((option) => (
            <MenuItem
              key={option}
              value={option}
              disabled={!telemetry && option === "CottonCloud"}
              sx={{ alignItems: "flex-start", py: 1 }}
            >
              <Stack spacing={0.25}>
                <Typography variant="body2">
                  {t(`settings.general.geoIpLookupMode.${option}`)}
                </Typography>
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ whiteSpace: "normal", lineHeight: 1.35 }}
                >
                  {t(`settings.general.geoIpLookupModeDescription.${option}`)}
                </Typography>
              </Stack>
            </MenuItem>
          ))}
        </TextField>
        {mode === "CustomHttp" && (
          <TextField
            label={t("settings.general.fields.customGeoIpLookupUrl")}
            value={url}
            onChange={(event) => {
              setUrl(event.target.value);
              setUrlTouched(true);
            }}
            onBlur={() => void commitUrl()}
            onKeyDown={handleUrlKeyDown}
            disabled={disabled}
            error={Boolean(urlError)}
            helperText={urlError ?? " "}
            fullWidth
          />
        )}
      </Stack>
    </SettingsSection>
  );
};
