import {
  Alert,
  Box,
  Button,
  Checkbox,
  FormControl,
  FormControlLabel,
  InputLabel,
  LinearProgress,
  MenuItem,
  Paper,
  Select,
  Stack,
  Switch,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  settingsApi,
  type ComputionMode,
  type GeoIpLookupMode,
  type ServerUsage,
  type StorageSpaceMode,
} from "../../../shared/api/settingsApi";

type LoadState =
  | { kind: "loading" }
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "error"; message: string }
  | { kind: "success"; message: string };

const usageOptions: ServerUsage[] = ["Photos", "Documents", "Media", "Other"];
const computionOptions: ComputionMode[] = ["Local", "Remote", "Cloud"];
const storageSpaceOptions: StorageSpaceMode[] = [
  "Optimal",
  "Limited",
  "Unlimited",
];
const geoIpOptions: GeoIpLookupMode[] = ["Disabled", "CottonCloud", "CustomHttp"];

export const AdminGeneralSettingsPage = () => {
  const { t } = useTranslation("admin");
  const [loadState, setLoadState] = useState<LoadState>({ kind: "loading" });
  const [publicBaseUrl, setPublicBaseUrl] = useState("");
  const [timezone, setTimezone] = useState("UTC");
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

  const isBusy = loadState.kind === "loading" || loadState.kind === "saving";

  useEffect(() => {
    let active = true;

    const load = async () => {
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

        setPublicBaseUrl(nextPublicBaseUrl);
        setTimezone(nextTimezone);
        setTelemetry(nextTelemetry);
        setAllowDeduplication(nextAllowDeduplication);
        setAllowGlobalIndexing(nextAllowGlobalIndexing);
        setServerUsage(nextServerUsage);
        setStorageSpaceMode(nextStorageSpaceMode);
        setComputionMode(nextComputionMode);
        setGeoIpLookupMode(nextGeoIpLookupMode);
        setCustomGeoIpLookupUrl(nextCustomGeoIpLookupUrl);
        setLoadState({ kind: "idle" });
      } catch {
        if (!active) return;
        setLoadState({
          kind: "error",
          message: t("settings.errors.loadFailed"),
        });
      }
    };

    void load();

    return () => {
      active = false;
    };
  }, [t]);

  const toggleUsage = (usage: ServerUsage) => {
    setServerUsage((current) => {
      const next = current.includes(usage)
        ? current.filter((item) => item !== usage)
        : [...current, usage];
      return next.length > 0 ? next : ["Other"];
    });
  };

  const handleSave = async () => {
    setLoadState({ kind: "saving" });
    try {
      await settingsApi.setTelemetry(telemetry);
      await settingsApi.setPublicBaseUrl(publicBaseUrl);
      await settingsApi.setTimezone(timezone);
      await settingsApi.setAllowCrossUserDeduplication(allowDeduplication);
      await settingsApi.setAllowGlobalIndexing(allowGlobalIndexing);
      await settingsApi.setServerUsage(serverUsage);
      await settingsApi.setStorageSpaceMode(storageSpaceMode);
      await settingsApi.setComputionMode(computionMode);
      if (geoIpLookupMode === "CustomHttp") {
        await settingsApi.setCustomGeoIpLookupUrl(customGeoIpLookupUrl);
      }
      await settingsApi.setGeoIpLookupMode(geoIpLookupMode);

      setLoadState({
        kind: "success",
        message: t("settings.state.saved"),
      });
    } catch {
      setLoadState({
        kind: "error",
        message: t("settings.errors.saveFailed"),
      });
    }
  };

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
                opacity: isBusy ? 1 : 0,
                transition: "opacity 120ms ease",
              }}
            />
          </Box>

          {loadState.kind === "error" && (
            <Alert severity="error">{loadState.message}</Alert>
          )}
          {loadState.kind === "success" && (
            <Alert severity="success">{loadState.message}</Alert>
          )}

          <Stack spacing={2}>
            <TextField
              label={t("settings.general.fields.publicBaseUrl")}
              value={publicBaseUrl}
              onChange={(event) => setPublicBaseUrl(event.target.value)}
              disabled={isBusy}
              fullWidth
            />
            <TextField
              label={t("settings.general.fields.timezone")}
              value={timezone}
              onChange={(event) => setTimezone(event.target.value)}
              disabled={isBusy}
              fullWidth
            />

            <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
              <FormControl fullWidth>
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
                  disabled={isBusy}
                >
                  {computionOptions.map((option) => (
                    <MenuItem
                      key={option}
                      value={option}
                      disabled={!telemetry && option === "Cloud"}
                    >
                      {t(`settings.general.computionMode.${option}`)}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              <FormControl fullWidth>
                <InputLabel id="admin-storage-space-mode-label">
                  {t("settings.general.fields.storageSpaceMode")}
                </InputLabel>
                <Select
                  labelId="admin-storage-space-mode-label"
                  label={t("settings.general.fields.storageSpaceMode")}
                  value={storageSpaceMode}
                  onChange={(event) =>
                    setStorageSpaceMode(event.target.value as StorageSpaceMode)
                  }
                  disabled={isBusy}
                >
                  {storageSpaceOptions.map((option) => (
                    <MenuItem key={option} value={option}>
                      {t(`settings.general.storageSpaceMode.${option}`)}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Stack>

            <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
              <FormControlLabel
                control={
                  <Switch
                    checked={telemetry}
                    onChange={(event) => setTelemetry(event.target.checked)}
                    disabled={isBusy}
                  />
                }
                label={t("settings.general.fields.telemetry")}
              />
              <FormControlLabel
                control={
                  <Switch
                    checked={allowDeduplication}
                    onChange={(event) =>
                      setAllowDeduplication(event.target.checked)
                    }
                    disabled={isBusy}
                  />
                }
                label={t("settings.general.fields.allowDeduplication")}
              />
              <FormControlLabel
                control={
                  <Switch
                    checked={allowGlobalIndexing}
                    onChange={(event) =>
                      setAllowGlobalIndexing(event.target.checked)
                    }
                    disabled={isBusy}
                  />
                }
                label={t("settings.general.fields.allowGlobalIndexing")}
              />
            </Stack>

            <Stack spacing={1}>
              <Typography variant="subtitle2" fontWeight={700}>
                {t("settings.general.fields.serverUsage")}
              </Typography>
              <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
                {usageOptions.map((option) => (
                  <FormControlLabel
                    key={option}
                    control={
                      <Checkbox
                        checked={serverUsage.includes(option)}
                        onChange={() => toggleUsage(option)}
                        disabled={isBusy}
                      />
                    }
                    label={t(`settings.general.serverUsage.${option}`)}
                  />
                ))}
              </Stack>
            </Stack>

            <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
              <FormControl fullWidth>
                <InputLabel id="admin-geoip-mode-label">
                  {t("settings.general.fields.geoIpLookupMode")}
                </InputLabel>
                <Select
                  labelId="admin-geoip-mode-label"
                  label={t("settings.general.fields.geoIpLookupMode")}
                  value={geoIpLookupMode}
                  onChange={(event) =>
                    setGeoIpLookupMode(event.target.value as GeoIpLookupMode)
                  }
                  disabled={isBusy}
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

              <TextField
                label={t("settings.general.fields.customGeoIpLookupUrl")}
                value={customGeoIpLookupUrl}
                onChange={(event) =>
                  setCustomGeoIpLookupUrl(event.target.value)
                }
                disabled={isBusy || geoIpLookupMode !== "CustomHttp"}
                fullWidth
              />
            </Stack>
          </Stack>

          <Stack direction="row" justifyContent="flex-end">
            <Button
              variant="contained"
              onClick={handleSave}
              disabled={isBusy}
            >
              {loadState.kind === "saving"
                ? t("settings.actions.saving")
                : t("settings.actions.save")}
            </Button>
          </Stack>
        </Stack>
      </Paper>
    </Stack>
  );
};
