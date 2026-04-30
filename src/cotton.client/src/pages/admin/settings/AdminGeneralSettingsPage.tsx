import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Checkbox,
  FormControl,
  FormControlLabel,
  IconButton,
  InputLabel,
  LinearProgress,
  MenuItem,
  Paper,
  Select,
  Stack,
  Switch,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from "@mui/material";
import { HelpOutline } from "@mui/icons-material";
import { useConfirm } from "material-ui-confirm";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  settingsApi,
  type ComputionMode,
  type GeoIpLookupMode,
  type ServerUsage,
  type StorageSpaceMode,
} from "../../../shared/api/settingsApi";
import { AdminSettingSavingOverlay } from "./AdminSettingSavingOverlay";
import {
  computionOptions,
  geoIpOptions,
  getSupportedTimeZones,
  isSameArray,
  normalizeStoredPublicBaseUrl,
  storageSpaceOptions,
  usageOptions,
  validateCustomGeoIpLookupUrl,
  validatePublicBaseUrl,
  validateTimezone,
  type GeneralSettingKey,
  type SettingsStatusMessage,
} from "./adminGeneralSettingsModel";

export const AdminGeneralSettingsPage = () => {
  const { t } = useTranslation("admin");
  const confirm = useConfirm();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [status, setStatus] = useState<SettingsStatusMessage | null>(null);
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
  const [storageSpaceMode, setStorageSpaceMode] =
    useState<StorageSpaceMode>("Optimal");
  const [computionMode, setComputionMode] = useState<ComputionMode>("Local");
  const [geoIpLookupMode, setGeoIpLookupMode] =
    useState<GeoIpLookupMode>("Disabled");
  const [customGeoIpLookupUrl, setCustomGeoIpLookupUrl] = useState("");
  const [savedCustomGeoIpLookupUrl, setSavedCustomGeoIpLookupUrl] = useState("");

  const timeZoneOptions = useMemo(() => getSupportedTimeZones(), []);
  const validTimeZones = useMemo(
    () => new Set(timeZoneOptions),
    [timeZoneOptions],
  );
  const currentOrigin = useMemo(
    () => (typeof window === "undefined" ? "" : window.location.origin),
    [],
  );

  const savingAny = savingKeys.size > 0;
  const pageDisabled = loading || loadError !== null;
  const isSaving = useCallback(
    (key: GeneralSettingKey): boolean => savingKeys.has(key),
    [savingKeys],
  );

  const setKeySaving = useCallback((key: GeneralSettingKey, saving: boolean) => {
    setSavingKeys((current) => {
      const next = new Set(current);
      if (saving) {
        next.add(key);
      } else {
        next.delete(key);
      }
      return next;
    });
  }, []);

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
      setStatus(null);
      setKeySaving(key, true);
      try {
        await task();
        options?.onSuccess?.();
        if (options?.showSuccess) {
          setStatus({
            severity: "success",
            message: t("settings.state.saved"),
          });
        }
      } catch {
        options?.onError?.();
        setStatus({
          severity: "error",
          message: t("settings.errors.saveFailed"),
        });
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
      setStatus(null);

      try {
        const [
          nextPublicBaseUrl,
          nextTimezone,
          nextTelemetry,
          nextAllowDeduplication,
          nextAllowGlobalIndexing,
          nextServerUsage,
          nextStorageSpaceMode,
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
          settingsApi.getStorageSpaceMode(),
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
        setServerUsage(nextServerUsage.length > 0 ? nextServerUsage : ["Other"]);
        setStorageSpaceMode(nextStorageSpaceMode);
        setComputionMode(nextComputionMode);
        setGeoIpLookupMode(nextGeoIpLookupMode);
        setCustomGeoIpLookupUrl(normalizedCustomGeoIpLookupUrl);
        setSavedCustomGeoIpLookupUrl(normalizedCustomGeoIpLookupUrl);
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
      customGeoIpLookupUrlRequiresIp: t(
        "settings.general.validation.customGeoIpLookupUrlRequiresIp",
      ),
    }),
    [t],
  );

  const publicBaseUrlValidation = useMemo(
    () => validatePublicBaseUrl(publicBaseUrl, currentOrigin, validationMessages),
    [currentOrigin, publicBaseUrl, validationMessages],
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
    customGeoIpLookupUrlValidation.normalized !== savedCustomGeoIpLookupUrl;

  const updatePublicBaseUrl = useCallback((value: string) => {
    setStatus(null);
    setPublicBaseUrl(value);
  }, []);

  const updateTimezone = useCallback((value: string) => {
    setStatus(null);
    setTimezone(value);
  }, []);

  const updateCustomGeoIpLookupUrl = useCallback((value: string) => {
    setStatus(null);
    setCustomGeoIpLookupUrl(value);
  }, []);

  const savePublicBaseUrl = useCallback(() => {
    const next = publicBaseUrlValidation.normalized;
    if (!next || !canSavePublicBaseUrl) return;

    setPublicBaseUrl(next);
    void runSave(
      "publicBaseUrl",
      () => settingsApi.setPublicBaseUrl(next),
      {
        onSuccess: () => setSavedPublicBaseUrl(next),
        showSuccess: true,
      },
    );
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
    const next = customGeoIpLookupUrlValidation.normalized;
    if (!next || !canSaveCustomGeoIpLookupUrl) return;

    setCustomGeoIpLookupUrl(next);
    void runSave(
      "customGeoIpLookupUrl",
      () => settingsApi.setCustomGeoIpLookupUrl(next),
      {
        onSuccess: () => setSavedCustomGeoIpLookupUrl(next),
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

  const handleStorageSpaceModeChange = useCallback(
    (_: unknown, next: StorageSpaceMode | null) => {
      if (!next || next === storageSpaceMode || pageDisabled) return;

      const previous = storageSpaceMode;
      setStorageSpaceMode(next);
      void runSave(
        "storageSpaceMode",
        () => settingsApi.setStorageSpaceMode(next),
        {
          onError: () => setStorageSpaceMode(previous),
        },
      );
    },
    [pageDisabled, runSave, storageSpaceMode],
  );

  const handleGeoIpLookupModeChange = useCallback(
    (next: GeoIpLookupMode) => {
      if (next === geoIpLookupMode || pageDisabled) return;

      const previous = geoIpLookupMode;
      setGeoIpLookupMode(next);
      void runSave(
        "geoIpLookupMode",
        () => settingsApi.setGeoIpLookupMode(next),
        {
          onError: () => setGeoIpLookupMode(previous),
        },
      );
    },
    [geoIpLookupMode, pageDisabled, runSave],
  );

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

  const showStorageSpaceHelp = useCallback(() => {
    void confirm({
      title: t("settings.general.storageSpaceHelp.title"),
      description: t("settings.general.storageSpaceHelp.description"),
      confirmationText: t("settings.actions.close"),
      hideCancelButton: true,
    });
  }, [confirm, t]);

  return (
    <Stack spacing={2}>
      <Paper>
        <Stack p={2} spacing={2}>
          <Stack spacing={0.5}>
            <Typography variant="h6" fontWeight={700}>
              {t("settings.general.title")}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {t("settings.general.description")}
            </Typography>
          </Stack>

          <Box minHeight={4}>
            <LinearProgress
              sx={{
                opacity: loading || savingAny ? 1 : 0,
                transition: "opacity 120ms ease",
              }}
            />
          </Box>

          {loadError && <Alert severity="error">{loadError}</Alert>}
          {status && <Alert severity={status.severity}>{status.message}</Alert>}

          <Stack spacing={2}>
            <Stack spacing={1}>
              <Stack
                direction={{ xs: "column", md: "row" }}
                spacing={1}
                alignItems={{ xs: "stretch", md: "flex-start" }}
              >
                <Box flex={1}>
                  <AdminSettingSavingOverlay saving={isSaving("publicBaseUrl")}>
                    <TextField
                      label={t("settings.general.fields.publicBaseUrl")}
                      value={publicBaseUrl}
                      onChange={(event) =>
                        updatePublicBaseUrl(event.target.value)
                      }
                      disabled={pageDisabled || isSaving("publicBaseUrl")}
                      error={Boolean(publicBaseUrlValidation.error)}
                      helperText={publicBaseUrlValidation.error ?? " "}
                      fullWidth
                    />
                  </AdminSettingSavingOverlay>
                </Box>
                <AdminSettingSavingOverlay saving={isSaving("publicBaseUrl")}>
                  <Button
                    variant="contained"
                    onClick={savePublicBaseUrl}
                    disabled={!canSavePublicBaseUrl}
                    sx={{ minWidth: 120, minHeight: 56 }}
                  >
                    {t("settings.actions.save")}
                  </Button>
                </AdminSettingSavingOverlay>
              </Stack>
              {publicBaseUrlValidation.mismatchesCurrentOrigin && (
                <Alert severity="warning">
                  {t("settings.general.validation.publicBaseUrlMismatch", {
                    current: currentOrigin,
                    configured: publicBaseUrlValidation.configuredOrigin,
                  })}
                </Alert>
              )}
            </Stack>

            <Stack
              direction={{ xs: "column", md: "row" }}
              spacing={1}
              alignItems={{ xs: "stretch", md: "flex-start" }}
            >
              <Box flex={1}>
                <AdminSettingSavingOverlay saving={isSaving("timezone")}>
                  <Autocomplete
                    freeSolo
                    options={timeZoneOptions}
                    value={timezone}
                    inputValue={timezone}
                    onChange={(_, value) => updateTimezone(value ?? "")}
                    onInputChange={(_, value) => updateTimezone(value)}
                    disabled={pageDisabled || isSaving("timezone")}
                    renderInput={(params) => (
                      <TextField
                        {...params}
                        label={t("settings.general.fields.timezone")}
                        error={Boolean(timezoneValidationError)}
                        helperText={timezoneValidationError ?? " "}
                      />
                    )}
                  />
                </AdminSettingSavingOverlay>
              </Box>
              <AdminSettingSavingOverlay saving={isSaving("timezone")}>
                <Button
                  variant="contained"
                  onClick={saveTimezone}
                  disabled={!canSaveTimezone}
                  sx={{ minWidth: 120, minHeight: 56 }}
                >
                  {t("settings.actions.save")}
                </Button>
              </AdminSettingSavingOverlay>
            </Stack>

            <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
              <Tooltip title={t("settings.general.computionMode.inDevelopment")}>
                <Box flex={1}>
                  <FormControl fullWidth disabled>
                    <InputLabel id="admin-compution-mode-label">
                      {t("settings.general.fields.computionMode")}
                    </InputLabel>
                    <Select
                      labelId="admin-compution-mode-label"
                      label={t("settings.general.fields.computionMode")}
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
                </Box>
              </Tooltip>

              <Stack flex={1} spacing={1}>
                <Stack direction="row" alignItems="center" spacing={0.5}>
                  <Typography variant="subtitle2" fontWeight={700}>
                    {t("settings.general.fields.storageSpaceMode")}
                  </Typography>
                  <Tooltip title={t("settings.general.storageSpaceHelp.open")}>
                    <IconButton
                      size="small"
                      onClick={showStorageSpaceHelp}
                      disabled={pageDisabled}
                    >
                      <HelpOutline fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </Stack>
                <AdminSettingSavingOverlay saving={isSaving("storageSpaceMode")}>
                  <ToggleButtonGroup
                    fullWidth
                    exclusive
                    value={storageSpaceMode}
                    onChange={handleStorageSpaceModeChange}
                    disabled={pageDisabled || isSaving("storageSpaceMode")}
                    aria-label={t("settings.general.fields.storageSpaceMode")}
                  >
                    {storageSpaceOptions.map((option) => (
                      <ToggleButton
                        key={option}
                        value={option}
                        aria-label={t(`settings.general.storageSpaceMode.${option}`)}
                      >
                        {t(`settings.general.storageSpaceMode.${option}`)}
                      </ToggleButton>
                    ))}
                  </ToggleButtonGroup>
                </AdminSettingSavingOverlay>
              </Stack>
            </Stack>

            <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
              <AdminSettingSavingOverlay saving={isSaving("telemetry")}>
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
                  label={t("settings.general.fields.telemetry")}
                />
              </AdminSettingSavingOverlay>
              <AdminSettingSavingOverlay saving={isSaving("allowDeduplication")}>
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
                  label={t("settings.general.fields.allowDeduplication")}
                />
              </AdminSettingSavingOverlay>
              <AdminSettingSavingOverlay saving={isSaving("allowGlobalIndexing")}>
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
                  label={t("settings.general.fields.allowGlobalIndexing")}
                />
              </AdminSettingSavingOverlay>
            </Stack>

            <Stack spacing={1}>
              <Typography variant="subtitle2" fontWeight={700}>
                {t("settings.general.fields.serverUsage")}
              </Typography>
              <AdminSettingSavingOverlay saving={isSaving("serverUsage")}>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
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

            <Stack spacing={2}>
              <AdminSettingSavingOverlay saving={isSaving("geoIpLookupMode")}>
                <FormControl fullWidth>
                  <InputLabel id="admin-geoip-mode-label">
                    {t("settings.general.fields.geoIpLookupMode")}
                  </InputLabel>
                  <Select
                    labelId="admin-geoip-mode-label"
                    label={t("settings.general.fields.geoIpLookupMode")}
                    value={geoIpLookupMode}
                    onChange={(event) =>
                      handleGeoIpLookupModeChange(
                        event.target.value as GeoIpLookupMode,
                      )
                    }
                    disabled={pageDisabled || isSaving("geoIpLookupMode")}
                  >
                    {geoIpOptions.map((option) => (
                      <MenuItem
                        key={option}
                        value={option}
                        disabled={!telemetry && option === "CottonCloud"}
                      >
                        {t(`settings.general.geoIpLookupMode.${option}`)}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              </AdminSettingSavingOverlay>

              {geoIpLookupMode === "CustomHttp" && (
                <Stack
                  direction={{ xs: "column", md: "row" }}
                  spacing={1}
                  alignItems={{ xs: "stretch", md: "flex-start" }}
                >
                  <Box flex={1}>
                    <AdminSettingSavingOverlay saving={isSaving("customGeoIpLookupUrl")}>
                      <TextField
                        label={t("settings.general.fields.customGeoIpLookupUrl")}
                        value={customGeoIpLookupUrl}
                        onChange={(event) =>
                          updateCustomGeoIpLookupUrl(event.target.value)
                        }
                        disabled={
                          pageDisabled || isSaving("customGeoIpLookupUrl")
                        }
                        error={Boolean(customGeoIpLookupUrlValidation.error)}
                        helperText={customGeoIpLookupUrlValidation.error ?? " "}
                        fullWidth
                      />
                    </AdminSettingSavingOverlay>
                  </Box>
                  <AdminSettingSavingOverlay saving={isSaving("customGeoIpLookupUrl")}>
                    <Button
                      variant="contained"
                      onClick={saveCustomGeoIpLookupUrl}
                      disabled={!canSaveCustomGeoIpLookupUrl}
                      sx={{ minWidth: 120, minHeight: 56 }}
                    >
                      {t("settings.actions.save")}
                    </Button>
                  </AdminSettingSavingOverlay>
                </Stack>
              )}
            </Stack>
          </Stack>
        </Stack>
      </Paper>
    </Stack>
  );
};
