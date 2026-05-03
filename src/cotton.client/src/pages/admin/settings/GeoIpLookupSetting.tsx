import {
  MenuItem,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "react-toastify";
import {
  settingsApi,
  type GeoIpLookupMode,
} from "../../../shared/api/settingsApi";
import {
  hasApiErrorToastBeenDispatched,
  isAxiosError,
} from "../../../shared/api/httpClient";
import { SettingsSection } from "./SettingsSection";
import {
  geoIpOptions,
  validateCustomGeoIpLookupUrl,
  type GeneralSettingsValidationMessages,
} from "./adminGeneralSettingsModel";
import type { SaveStatus } from "./useAutoSavedSetting";

const SAVED_FLASH_MS = 1500;

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
    setStatus("loading");
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
    }, SAVED_FLASH_MS);
  }, []);

  const reportError = useCallback(
    (error: unknown) => {
      setStatus("error");
      if (!isAxiosError(error) || !hasApiErrorToastBeenDispatched(error)) {
        toast.error(t("settings.errors.saveFailed"), {
          toastId: "admin-general:geoip:save-error",
        });
      }
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

  const commitUrl = () => {
    setUrlTouched(true);
    const next = urlValidation.normalized;
    if (urlValidation.error || next === null) return;
    if (next === savedUrl && savedMode === "CustomHttp") return;

    setUrl(next);
    setStatus("saving");

    Promise.resolve()
      .then(() => settingsApi.setCustomGeoIpLookupUrl(next))
      .then(() =>
        savedMode === "CustomHttp"
          ? Promise.resolve()
          : settingsApi.setGeoIpLookupMode("CustomHttp"),
      )
      .then(() => {
        setSavedUrl(next);
        setSavedMode("CustomHttp");
        flashSaved();
      })
      .catch((error) => {
        setUrl(savedUrl);
        setMode(savedMode);
        reportError(error);
      });
  };

  const handleUrlKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter") {
      event.preventDefault();
      commitUrl();
    }
  };

  const disabled = status === "loading" || status === "saving";

  return (
    <SettingsSection
      title={t("settings.general.fields.geoIpLookupMode")}
      description={t(`settings.general.geoIpLookupModeDescription.${mode}`)}
      status={status}
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
            onBlur={commitUrl}
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
