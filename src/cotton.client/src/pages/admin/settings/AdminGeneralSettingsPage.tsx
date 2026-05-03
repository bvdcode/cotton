import {
  Alert,
  Autocomplete,
  Box,
  Checkbox,
  FormControl,
  FormControlLabel,
  InputAdornment,
  LinearProgress,
  MenuItem,
  Paper,
  Select,
  Stack,
  Switch,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import { HelpOutline } from "@mui/icons-material";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "react-toastify";
import {
  settingsApi,
  type ComputionMode,
  type GeoIpLookupMode,
  type ServerUsage,
} from "../../../shared/api/settingsApi";
import {
  hasApiErrorToastBeenDispatched,
  isAxiosError,
} from "../../../shared/api/httpClient";
import { AdminSettingSaveField } from "./AdminSettingSaveField";
import { AdminSettingSavingOverlay } from "./AdminSettingSavingOverlay";
import { AdminGeoIpLookupModeField } from "./AdminGeoIpLookupModeField";
import {
  computionOptions,
  getSupportedTimeZones,
  isSameArray,
  normalizeStoredPublicBaseUrl,
  usageOptions,
  validateCustomGeoIpLookupUrl,
  validatePublicBaseUrl,
  validateTimezone,
  type GeneralSettingKey,
} from "./adminGeneralSettingsModel";

const contentMaxWidth = 1080;

const SettingHelpIcon = ({ title }: { title: string }) => (
  <Tooltip title={title}>
    <Box
      component="span"
      aria-label={title}
      sx={{
        display: "inline-flex",
        alignItems: "center",
        color: "text.secondary",
        cursor: "help",
      }}
    >
      <HelpOutline fontSize="small" />
    </Box>
  </Tooltip>
);

const renderHelpLabel = (label: string, help: string) => (
  <Stack component="span" direction="row" alignItems="center" spacing={0.5}>
    <Box component="span">{label}</Box>
    <SettingHelpIcon title={help} />
  </Stack>
);

export const AdminGeneralSettingsPage = () => {
  const { t } = useTranslation("admin");

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [savingKeys, setSavingKeys] = useState<ReadonlySet<GeneralSettingKey>>(
    () => new Set(),
  );

  const [publicBaseUrl, setPublicBaseUrl] = useState("");
  const [savedPublicBaseUrl, setSavedPublicBaseUrl] = useState("");
  const [timezone, setTimezone] = useState("UTC");
  const [savedTimezone, setSavedTimezone] = useState("UTC");
  const [telemetry, setTelemetry] = useState(false);
  const [allowDeduplication, setAllowDeduplication] = useState(false);
  const [allowGlobalIndexing, setAllowGlobalIndexing] = useState(false);
  const [serverUsage, setServerUsage] = useState<ServerUsage[]>(["Other"]);
  const [computionMode, setComputionMode] = useState<ComputionMode>("Local");
  const [geoIpLookupMode, setGeoIpLookupMode] =
    useState<GeoIpLookupMode>("Disabled");
  const [savedGeoIpLookupMode, setSavedGeoIpLookupMode] =
    useState<GeoIpLookupMode>("Disabled");
  const [customGeoIpLookupUrl, setCustomGeoIpLookupUrl] = useState("");
  const [savedCustomGeoIpLookupUrl, setSavedCustomGeoIpLookupUrl] =
    useState("");
  const [customGeoIpLookupUrlTouched, setCustomGeoIpLookupUrlTouched] =
    useState(false);

  const timeZoneOptions = useMemo(() => getSupportedTimeZones(), []);
  const validTimeZones = useMemo(
    () => new Set(timeZoneOptions),
    [timeZoneOptions],
  );

  const pageDisabled = loading || loadError !== null;
  const isSaving = useCallback(
    (key: GeneralSettingKey): boolean => savingKeys.has(key),
    [savingKeys],
  );

  const setKeySaving = useCallback(
    (key: GeneralSettingKey, saving: boolean) => {
      setSavingKeys((current) => {
        const next = new Set(current);
        if (saving) {
          next.add(key);
        } else {
          next.delete(key);
        }
        return next;
      });
    },
    [],
  );

  const runSave = useCallback(
    async (
      key: GeneralSettingKey,
      task: () => Promise<void>,
      options?: {
        onSuccess?: () => void;
        onError?: () => void;
        showSuccess?: boolean;
      },
    ) => {
      setKeySaving(key, true);
      try {
        await task();
        options?.onSuccess?.();
        if (options?.showSuccess) {
          toast.success(t("settings.state.saved"), {
            toastId: `admin-general-settings:${key}:saved`,
          });
        }
      } catch (error) {
        options?.onError?.();
        if (!isAxiosError(error) || !hasApiErrorToastBeenDispatched(error)) {
          toast.error(t("settings.errors.saveFailed"), {
            toastId: `admin-general-settings:${key}:save-failed`,
          });
        }
      } finally {
        setKeySaving(key, false);
      }
    },
    [setKeySaving, t],
  );

  useEffect(() => {
    let active = true;

    const load = async () => {
      setLoading(true);
      setLoadError(null);

      try {
        const [
          nextPublicBaseUrl,
          nextTimezone,
          nextTelemetry,
          nextAllowDeduplication,
          nextAllowGlobalIndexing,
          nextServerUsage,
          nextComputionMode,
          nextGeoIpLookupMode,
          nextCustomGeoIpLookupUrl,
        ] = await Promise.all([
          settingsApi.getPublicBaseUrl(),
          settingsApi.getTimezone(),
          settingsApi.getTelemetry(),
          settingsApi.getAllowCrossUserDeduplication(),
          settingsApi.getAllowGlobalIndexing(),
          settingsApi.getServerUsage(),
          settingsApi.getComputionMode(),
          settingsApi.getGeoIpLookupMode(),
          settingsApi.getCustomGeoIpLookupUrl(),
        ]);

        if (!active) return;

        const normalizedPublicBaseUrl =
          normalizeStoredPublicBaseUrl(nextPublicBaseUrl);
        const normalizedTimezone = nextTimezone.trim() || "UTC";
        const normalizedCustomGeoIpLookupUrl = nextCustomGeoIpLookupUrl.trim();

        setPublicBaseUrl(normalizedPublicBaseUrl);
        setSavedPublicBaseUrl(normalizedPublicBaseUrl);
        setTimezone(normalizedTimezone);
        setSavedTimezone(normalizedTimezone);
        setTelemetry(nextTelemetry);
        setAllowDeduplication(nextAllowDeduplication);
        setAllowGlobalIndexing(nextAllowGlobalIndexing);
        setServerUsage(
          nextServerUsage.length > 0 ? nextServerUsage : ["Other"],
        );
        setComputionMode(nextComputionMode);
        setGeoIpLookupMode(nextGeoIpLookupMode);
        setSavedGeoIpLookupMode(nextGeoIpLookupMode);
        setCustomGeoIpLookupUrl(normalizedCustomGeoIpLookupUrl);
        setSavedCustomGeoIpLookupUrl(normalizedCustomGeoIpLookupUrl);
        setCustomGeoIpLookupUrlTouched(false);
      } catch {
        if (!active) return;
        setLoadError(t("settings.errors.loadFailed"));
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    };

    void load();

    return () => {
      active = false;
    };
  }, [t]);

  const validationMessages = useMemo(
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

  const publicBaseUrlValidation = useMemo(
    () => validatePublicBaseUrl(publicBaseUrl, validationMessages),
    [publicBaseUrl, validationMessages],
  );

  const timezoneValidationError = useMemo(
    () => validateTimezone(timezone, validTimeZones, validationMessages),
    [timezone, validTimeZones, validationMessages],
  );

  const customGeoIpLookupUrlValidation = useMemo(
    () =>
      validateCustomGeoIpLookupUrl(
        customGeoIpLookupUrl,
        geoIpLookupMode === "CustomHttp",
        validationMessages,
      ),
    [customGeoIpLookupUrl, geoIpLookupMode, validationMessages],
  );

  const canSavePublicBaseUrl =
    !pageDisabled &&
    !isSaving("publicBaseUrl") &&
    !publicBaseUrlValidation.error &&
    publicBaseUrlValidation.normalized !== null &&
    publicBaseUrlValidation.normalized !== savedPublicBaseUrl;

  const canSaveTimezone =
    !pageDisabled &&
    !isSaving("timezone") &&
    !timezoneValidationError &&
    timezone.trim() !== savedTimezone;

  const canSaveCustomGeoIpLookupUrl =
    geoIpLookupMode === "CustomHttp" &&
    !pageDisabled &&
    !isSaving("customGeoIpLookupUrl") &&
    !customGeoIpLookupUrlValidation.error &&
    customGeoIpLookupUrlValidation.normalized !== null &&
    (customGeoIpLookupUrlValidation.normalized !== savedCustomGeoIpLookupUrl ||
      savedGeoIpLookupMode !== "CustomHttp");

  const updatePublicBaseUrl = useCallback((value: string) => {
    setPublicBaseUrl(value);
  }, []);

  const updateTimezone = useCallback((value: string) => {
    setTimezone(value);
  }, []);

  const updateCustomGeoIpLookupUrl = useCallback((value: string) => {
    setCustomGeoIpLookupUrl(value);
    setCustomGeoIpLookupUrlTouched(true);
  }, []);

  const getGeoIpModeLabel = useCallback(
    (mode: GeoIpLookupMode) => t(`settings.general.geoIpLookupMode.${mode}`),
    [t],
  );

  const getGeoIpModeDescription = useCallback(
    (mode: GeoIpLookupMode) =>
      t(`settings.general.geoIpLookupModeDescription.${mode}`),
    [t],
  );

  const savePublicBaseUrl = useCallback(() => {
    const next = publicBaseUrlValidation.normalized;
    if (!next || !canSavePublicBaseUrl) return;

    setPublicBaseUrl(next);
    void runSave("publicBaseUrl", () => settingsApi.setPublicBaseUrl(next), {
      onSuccess: () => setSavedPublicBaseUrl(next),
      showSuccess: true,
    });
  }, [canSavePublicBaseUrl, publicBaseUrlValidation.normalized, runSave]);

  const saveTimezone = useCallback(() => {
    const next = timezone.trim();
    if (!canSaveTimezone) return;

    setTimezone(next);
    void runSave("timezone", () => settingsApi.setTimezone(next), {
      onSuccess: () => setSavedTimezone(next),
      showSuccess: true,
    });
  }, [canSaveTimezone, runSave, timezone]);

  const saveCustomGeoIpLookupUrl = useCallback(() => {
    setCustomGeoIpLookupUrlTouched(true);
    const next = customGeoIpLookupUrlValidation.normalized;
    if (!next || !canSaveCustomGeoIpLookupUrl) return;

    setCustomGeoIpLookupUrl(next);
    void runSave(
      "customGeoIpLookupUrl",
      async () => {
        await settingsApi.setCustomGeoIpLookupUrl(next);
        await settingsApi.setGeoIpLookupMode("CustomHttp");
      },
      {
        onSuccess: () => {
          setSavedCustomGeoIpLookupUrl(next);
          setSavedGeoIpLookupMode("CustomHttp");
        },
        showSuccess: true,
      },
    );
  }, [
    canSaveCustomGeoIpLookupUrl,
    customGeoIpLookupUrlValidation.normalized,
    runSave,
  ]);

  const handleTelemetryChange = useCallback(
    (next: boolean) => {
      if (next === telemetry || pageDisabled) return;

      const previous = telemetry;
      setTelemetry(next);
      void runSave("telemetry", () => settingsApi.setTelemetry(next), {
        onError: () => setTelemetry(previous),
      });
    },
    [pageDisabled, runSave, telemetry],
  );

  const handleAllowDeduplicationChange = useCallback(
    (next: boolean) => {
      if (next === allowDeduplication || pageDisabled) return;

      const previous = allowDeduplication;
      setAllowDeduplication(next);
      void runSave(
        "allowDeduplication",
        () => settingsApi.setAllowCrossUserDeduplication(next),
        {
          onError: () => setAllowDeduplication(previous),
        },
      );
    },
    [allowDeduplication, pageDisabled, runSave],
  );

  const handleAllowGlobalIndexingChange = useCallback(
    (next: boolean) => {
      if (next === allowGlobalIndexing || pageDisabled) return;

      const previous = allowGlobalIndexing;
      setAllowGlobalIndexing(next);
      void runSave(
        "allowGlobalIndexing",
        () => settingsApi.setAllowGlobalIndexing(next),
        {
          onError: () => setAllowGlobalIndexing(previous),
        },
      );
    },
    [allowGlobalIndexing, pageDisabled, runSave],
  );

  const handleGeoIpLookupModeChange = useCallback(
    (next: GeoIpLookupMode) => {
      if (
        next === geoIpLookupMode ||
        pageDisabled ||
        isSaving("geoIpLookupMode") ||
        isSaving("customGeoIpLookupUrl")
      ) {
        return;
      }

      if (next === "CustomHttp") {
        setGeoIpLookupMode(next);
        setCustomGeoIpLookupUrlTouched(false);
        return;
      }

      const previous = geoIpLookupMode;
      setGeoIpLookupMode(next);
      void runSave(
        "geoIpLookupMode",
        () => settingsApi.setGeoIpLookupMode(next),
        {
          onSuccess: () => setSavedGeoIpLookupMode(next),
          onError: () => setGeoIpLookupMode(previous),
        },
      );
    },
    [geoIpLookupMode, isSaving, pageDisabled, runSave],
  );

  const customGeoIpLookupUrlError =
    customGeoIpLookupUrlTouched && geoIpLookupMode === "CustomHttp"
      ? customGeoIpLookupUrlValidation.error
      : null;

  const toggleUsage = useCallback(
    (usage: ServerUsage) => {
      if (pageDisabled) return;

      const previous = serverUsage;
      const toggled = serverUsage.includes(usage)
        ? serverUsage.filter((item) => item !== usage)
        : [...serverUsage, usage];
      const next =
        toggled.length > 0 ? toggled : (["Other"] satisfies ServerUsage[]);

      if (isSameArray(previous, next)) return;

      setServerUsage(next);
      void runSave("serverUsage", () => settingsApi.setServerUsage(next), {
        onError: () => setServerUsage(previous),
      });
    },
    [pageDisabled, runSave, serverUsage],
  );

  return (
    <Stack spacing={2}>
      <Paper sx={{ overflow: "hidden" }}>
        <Stack
          p={2}
          spacing={2}
          sx={{ maxWidth: contentMaxWidth, width: "100%", mx: "auto" }}
        >
          <Stack spacing={0.5}>
            <Typography variant="h6" fontWeight={700}>
              {t("settings.general.title")}
            </Typography>
          </Stack>

          <Box minHeight={4}>
            <LinearProgress
              sx={{
                opacity: loading ? 1 : 0,
                transition: "opacity 120ms ease",
              }}
            />
          </Box>

          {loadError && <Alert severity="error">{loadError}</Alert>}

          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "minmax(0, 1fr)",
                lg: "repeat(2, minmax(320px, 1fr))",
              },
              gap: 2,
              alignItems: "start",
            }}
          >
            <AdminSettingSaveField
              label={t("settings.actions.save")}
              onSave={savePublicBaseUrl}
              disabled={!canSavePublicBaseUrl}
              saving={isSaving("publicBaseUrl")}
            >
              <AdminSettingSavingOverlay saving={loading}>
                <TextField
                  label={t("settings.general.fields.publicBaseUrl")}
                  value={publicBaseUrl}
                  onChange={(event) => updatePublicBaseUrl(event.target.value)}
                  disabled={pageDisabled}
                  error={Boolean(publicBaseUrlValidation.error)}
                  helperText={publicBaseUrlValidation.error ?? " "}
                  fullWidth
                  InputProps={{
                    endAdornment: (
                      <InputAdornment position="end">
                        <SettingHelpIcon
                          title={t("settings.general.help.publicBaseUrl")}
                        />
                      </InputAdornment>
                    ),
                  }}
                />
              </AdminSettingSavingOverlay>
            </AdminSettingSaveField>

            <AdminSettingSaveField
              label={t("settings.actions.save")}
              onSave={saveTimezone}
              disabled={!canSaveTimezone}
              saving={isSaving("timezone")}
            >
              <AdminSettingSavingOverlay saving={loading}>
                <Autocomplete
                  freeSolo
                  options={timeZoneOptions}
                  value={timezone}
                  inputValue={timezone}
                  onChange={(_, value) => updateTimezone(value ?? "")}
                  onInputChange={(_, value) => updateTimezone(value)}
                  disabled={pageDisabled}
                  renderInput={(params) => (
                    <TextField
                      {...params}
                      label={t("settings.general.fields.timezone")}
                      error={Boolean(timezoneValidationError)}
                      helperText={timezoneValidationError ?? " "}
                      InputProps={{
                        ...params.InputProps,
                        endAdornment: (
                          <>
                            <InputAdornment position="end">
                              <SettingHelpIcon
                                title={t("settings.general.help.timezone")}
                              />
                            </InputAdornment>
                            {params.InputProps.endAdornment}
                          </>
                        ),
                      }}
                    />
                  )}
                />
              </AdminSettingSavingOverlay>
            </AdminSettingSaveField>

            <Stack spacing={1}>
              <Stack
                direction="row"
                alignItems="center"
                spacing={0.5}
                minHeight={32}
              >
                <Typography variant="subtitle2" fontWeight={700}>
                  {t("settings.general.fields.computionMode")}
                </Typography>
                <SettingHelpIcon
                  title={t("settings.general.computionMode.inDevelopment")}
                />
              </Stack>
              <AdminSettingSavingOverlay saving={loading}>
                <FormControl fullWidth disabled>
                  <Select
                    value={computionMode}
                    onChange={(event) =>
                      setComputionMode(event.target.value as ComputionMode)
                    }
                  >
                    {computionOptions.map((option) => (
                      <MenuItem key={option} value={option}>
                        {t(`settings.general.computionMode.${option}`)}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              </AdminSettingSavingOverlay>
            </Stack>

            <Stack
              direction={{ xs: "column", md: "row" }}
              spacing={2}
              sx={{ gridColumn: { lg: "1 / -1" } }}
            >
              <AdminSettingSavingOverlay
                saving={loading || isSaving("telemetry")}
              >
                <FormControlLabel
                  control={
                    <Switch
                      checked={telemetry}
                      onChange={(event) =>
                        handleTelemetryChange(event.target.checked)
                      }
                      disabled={pageDisabled || isSaving("telemetry")}
                    />
                  }
                  label={renderHelpLabel(
                    t("settings.general.fields.telemetry"),
                    t("settings.general.help.telemetry"),
                  )}
                />
              </AdminSettingSavingOverlay>
              <AdminSettingSavingOverlay
                saving={loading || isSaving("allowDeduplication")}
              >
                <FormControlLabel
                  control={
                    <Switch
                      checked={allowDeduplication}
                      onChange={(event) =>
                        handleAllowDeduplicationChange(event.target.checked)
                      }
                      disabled={pageDisabled || isSaving("allowDeduplication")}
                    />
                  }
                  label={renderHelpLabel(
                    t("settings.general.fields.allowDeduplication"),
                    t("settings.general.help.allowDeduplication"),
                  )}
                />
              </AdminSettingSavingOverlay>
              <AdminSettingSavingOverlay
                saving={loading || isSaving("allowGlobalIndexing")}
              >
                <FormControlLabel
                  control={
                    <Switch
                      checked={allowGlobalIndexing}
                      onChange={(event) =>
                        handleAllowGlobalIndexingChange(event.target.checked)
                      }
                      disabled={pageDisabled || isSaving("allowGlobalIndexing")}
                    />
                  }
                  label={renderHelpLabel(
                    t("settings.general.fields.allowGlobalIndexing"),
                    t("settings.general.help.allowGlobalIndexing"),
                  )}
                />
              </AdminSettingSavingOverlay>
            </Stack>

            <Stack spacing={1} sx={{ gridColumn: { lg: "1 / -1" } }}>
              <Stack direction="row" alignItems="center" spacing={0.5}>
                <Typography variant="subtitle2" fontWeight={700}>
                  {t("settings.general.fields.serverUsage")}
                </Typography>
                <SettingHelpIcon
                  title={t("settings.general.help.serverUsage")}
                />
              </Stack>
              <AdminSettingSavingOverlay
                saving={loading || isSaving("serverUsage")}
              >
                <Stack
                  direction={{ xs: "column", sm: "row" }}
                  spacing={1}
                  useFlexGap
                  sx={{ flexWrap: "wrap" }}
                >
                  {usageOptions.map((option) => (
                    <FormControlLabel
                      key={option}
                      control={
                        <Checkbox
                          checked={serverUsage.includes(option)}
                          onChange={() => toggleUsage(option)}
                          disabled={pageDisabled || isSaving("serverUsage")}
                        />
                      }
                      label={t(`settings.general.serverUsage.${option}`)}
                    />
                  ))}
                </Stack>
              </AdminSettingSavingOverlay>
            </Stack>

            <Stack
              spacing={1.5}
              sx={{
                gridColumn: { lg: "1 / -1" },
                maxWidth: { lg: 980 },
              }}
            >
              <AdminGeoIpLookupModeField
                value={geoIpLookupMode}
                loading={loading || isSaving("geoIpLookupMode")}
                disabled={pageDisabled || isSaving("geoIpLookupMode")}
                telemetryEnabled={telemetry}
                label={t("settings.general.fields.geoIpLookupMode")}
                getLabel={getGeoIpModeLabel}
                getDescription={getGeoIpModeDescription}
                onChange={handleGeoIpLookupModeChange}
              />

              {geoIpLookupMode === "CustomHttp" && (
                <Box sx={{ maxWidth: 760 }}>
                  <AdminSettingSaveField
                    label={t("settings.actions.save")}
                    onSave={saveCustomGeoIpLookupUrl}
                    disabled={!canSaveCustomGeoIpLookupUrl}
                    saving={isSaving("customGeoIpLookupUrl")}
                  >
                    <AdminSettingSavingOverlay saving={loading}>
                      <TextField
                        label={t("settings.general.fields.customGeoIpLookupUrl")}
                        value={customGeoIpLookupUrl}
                        onChange={(event) =>
                          updateCustomGeoIpLookupUrl(event.target.value)
                        }
                        disabled={pageDisabled}
                        error={Boolean(customGeoIpLookupUrlError)}
                        helperText={customGeoIpLookupUrlError ?? " "}
                        fullWidth
                      />
                    </AdminSettingSavingOverlay>
                  </AdminSettingSaveField>
                </Box>
              )}
            </Stack>
          </Box>
        </Stack>
      </Paper>
    </Stack>
  );
};
