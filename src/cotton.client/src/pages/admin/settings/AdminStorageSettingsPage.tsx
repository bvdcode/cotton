import SaveIcon from "@mui/icons-material/Save";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  MenuItem,
  Paper,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import {
  useEffect,
  useMemo,
  useState,
  type Dispatch,
  type SetStateAction,
} from "react";
import { useTranslation } from "react-i18next";
import {
  settingsApi,
  type S3Config,
  type StorageSpaceMode,
  type StorageType,
} from "../../../shared/api/settingsApi";
import { showApiErrorToast } from "../../../shared/api/httpClient";
import { SettingsSection } from "./SettingsSection";
import { storageSpaceOptions } from "./adminGeneralSettingsModel";
import type { SaveStatus } from "./useAutoSavedSetting";

const SAVED_FLASH_MS = 1500;
type FlashTimers = {
  storageType: number | null;
  s3: number | null;
  storageSpace: number | null;
};

const emptyS3Config: S3Config = {
  endpoint: "",
  region: "",
  bucket: "",
  accessKey: "",
  secretKey: "",
};

const flashStatus = (
  setStatus: Dispatch<SetStateAction<SaveStatus>>,
  flashTimers: FlashTimers,
  key: keyof FlashTimers,
) => {
  const pendingTimer = flashTimers[key];
  if (pendingTimer !== null) {
    window.clearTimeout(pendingTimer);
  }
  setStatus("saved");
  flashTimers[key] = window.setTimeout(() => {
    setStatus((current) => (current === "saved" ? "idle" : current));
    flashTimers[key] = null;
  }, SAVED_FLASH_MS);
};

export const AdminStorageSettingsPage = () => {
  const { t } = useTranslation("admin");

  const [loadError, setLoadError] = useState<string | null>(null);

  const [storageType, setStorageType] = useState<StorageType>("Local");
  const [savedStorageType, setSavedStorageType] = useState<StorageType>("Local");
  const [storageTypeStatus, setStorageTypeStatus] = useState<SaveStatus>("loading");

  const [s3Config, setS3Config] = useState<S3Config>(emptyS3Config);
  const [s3Status, setS3Status] = useState<SaveStatus>("loading");

  const [storageSpaceMode, setStorageSpaceMode] =
    useState<StorageSpaceMode>("Optimal");
  const [savedStorageSpaceMode, setSavedStorageSpaceMode] =
    useState<StorageSpaceMode>("Optimal");
  const [storageSpaceModeStatus, setStorageSpaceModeStatus] =
    useState<SaveStatus>("loading");

  const flashTimers = useMemo<FlashTimers>(
    () => ({
      storageType: null,
      s3: null,
      storageSpace: null,
    }),
    [],
  );

  useEffect(() => {
    let active = true;

    const load = async () => {
      setLoadError(null);
      setStorageTypeStatus("loading");
      setS3Status("loading");
      setStorageSpaceModeStatus("loading");

      try {
        const [nextStorageType, nextS3Config, nextStorageSpaceMode] =
          await Promise.all([
            settingsApi.getStorageType(),
            settingsApi.getS3Config(),
            settingsApi.getStorageSpaceMode(),
          ]);

        if (!active) return;

        setStorageType(nextStorageType);
        setSavedStorageType(nextStorageType);
        setS3Config(nextS3Config);
        setStorageSpaceMode(nextStorageSpaceMode);
        setSavedStorageSpaceMode(nextStorageSpaceMode);
        setStorageTypeStatus("idle");
        setS3Status("idle");
        setStorageSpaceModeStatus("idle");
      } catch {
        if (!active) return;
        setLoadError(t("storageSettings.errors.loadFailed"));
        setStorageTypeStatus("idle");
        setS3Status("idle");
        setStorageSpaceModeStatus("idle");
      }
    };

    void load();

    return () => {
      active = false;
      if (flashTimers.storageType !== null) {
        window.clearTimeout(flashTimers.storageType);
        flashTimers.storageType = null;
      }
      if (flashTimers.s3 !== null) {
        window.clearTimeout(flashTimers.s3);
        flashTimers.s3 = null;
      }
      if (flashTimers.storageSpace !== null) {
        window.clearTimeout(flashTimers.storageSpace);
        flashTimers.storageSpace = null;
      }
    };
  }, [flashTimers, t]);

  const updateS3Config = <K extends keyof S3Config>(
    key: K,
    value: S3Config[K],
  ) => {
    setS3Config((current) => ({ ...current, [key]: value }));
  };

  const handleStorageTypeChange = async (next: StorageType) => {
    if (
      next === storageType
      || storageTypeStatus === "loading"
      || storageTypeStatus === "saving"
      || s3Status === "saving"
    ) {
      return;
    }

    setStorageType(next);

    if (next === "S3") {
      setStorageTypeStatus("idle");
      return;
    }

    const previous = savedStorageType;
    setStorageTypeStatus("saving");

    try {
      await settingsApi.setStorageType(next);
      setSavedStorageType(next);
      flashStatus(setStorageTypeStatus, flashTimers, "storageType");
    } catch (error) {
      setStorageType(previous);
      setStorageTypeStatus("error");
      showApiErrorToast(
        error,
        t("storageSettings.errors.storageSaveFailed"),
        "admin-storage-settings:storage-type:save-failed",
      );
    }
  };

  const saveS3AndActivate = async () => {
    if (
      storageTypeStatus === "loading"
      || storageTypeStatus === "saving"
      || s3Status === "loading"
      || s3Status === "saving"
    ) {
      return;
    }

    setStorageTypeStatus("saving");
    setS3Status("saving");

    try {
      await settingsApi.setS3Config(s3Config);
      await settingsApi.setStorageType("S3");
      setStorageType("S3");
      setSavedStorageType("S3");
      flashStatus(setStorageTypeStatus, flashTimers, "storageType");
      flashStatus(setS3Status, flashTimers, "s3");
    } catch (error) {
      setStorageTypeStatus("error");
      setS3Status("error");
      showApiErrorToast(
        error,
        t("storageSettings.errors.storageSaveFailed"),
        "admin-storage-settings:s3:save-failed",
      );
    }
  };

  const handleStorageSpaceModeChange = async (
    next: StorageSpaceMode | null,
  ) => {
    if (
      !next
      || next === storageSpaceMode
      || storageSpaceModeStatus === "loading"
      || storageSpaceModeStatus === "saving"
    ) {
      return;
    }

    const previous = savedStorageSpaceMode;
    setStorageSpaceMode(next);
    setStorageSpaceModeStatus("saving");

    try {
      await settingsApi.setStorageSpaceMode(next);
      setSavedStorageSpaceMode(next);
      flashStatus(setStorageSpaceModeStatus, flashTimers, "storageSpace");
    } catch (error) {
      setStorageSpaceMode(previous);
      setStorageSpaceModeStatus("error");
      showApiErrorToast(
        error,
        t("settings.errors.saveFailed"),
        "admin-storage-settings:storage-space-mode:save-failed",
      );
    }
  };

  const storageTypeDisabled =
    storageTypeStatus === "loading"
    || storageTypeStatus === "saving"
    || s3Status === "saving";
  const s3Disabled = s3Status === "loading" || s3Status === "saving";
  const s3Saving = s3Status === "saving" || storageTypeStatus === "saving";
  const storageSpaceDisabled =
    storageSpaceModeStatus === "loading" || storageSpaceModeStatus === "saving";

  return (
    <Stack>
      <Box sx={{ width: "100%", display: "flex", justifyContent: "center" }}>
      <Paper
        sx={{
          width: "min(100%, 880px)",
          overflow: "hidden",
        }}
      >
        <Stack p={3} spacing={3} divider={<Divider flexItem />}>
          <Typography variant="h5" fontWeight={700}>
            {t("storageSettings.title")}
          </Typography>

          {loadError && <Alert severity="error">{loadError}</Alert>}

          <SettingsSection
            title={t("storageSettings.fields.storageType")}
            description={t("storageSettings.description")}
            status={storageTypeStatus}
          >
            <TextField
              select
              value={storageType}
              onChange={(event) =>
                void handleStorageTypeChange(event.target.value as StorageType)
              }
              disabled={storageTypeDisabled}
              fullWidth
            >
              <MenuItem value="Local">
                {t("storageSettings.storageType.Local")}
              </MenuItem>
              <MenuItem value="S3">
                {t("storageSettings.storageType.S3")}
              </MenuItem>
            </TextField>
          </SettingsSection>

          {storageType === "S3" && (
            <SettingsSection
              title={t("storageSettings.s3.title")}
              status={s3Status}
            >
              <Stack spacing={2}>
                <Box
                  sx={{
                    display: "grid",
                    gap: 2,
                    gridTemplateColumns: {
                      xs: "1fr",
                      md: "1fr 1fr",
                    },
                  }}
                >
                  <TextField
                    label={t("storageSettings.s3.fields.endpoint")}
                    value={s3Config.endpoint}
                    onChange={(event) =>
                      updateS3Config("endpoint", event.target.value)
                    }
                    disabled={s3Disabled}
                    fullWidth
                  />
                  <TextField
                    label={t("storageSettings.s3.fields.region")}
                    value={s3Config.region}
                    onChange={(event) =>
                      updateS3Config("region", event.target.value)
                    }
                    disabled={s3Disabled}
                    fullWidth
                  />
                  <TextField
                    label={t("storageSettings.s3.fields.bucket")}
                    value={s3Config.bucket}
                    onChange={(event) =>
                      updateS3Config("bucket", event.target.value)
                    }
                    disabled={s3Disabled}
                    fullWidth
                  />
                  <TextField
                    label={t("storageSettings.s3.fields.accessKey")}
                    value={s3Config.accessKey}
                    onChange={(event) =>
                      updateS3Config("accessKey", event.target.value)
                    }
                    disabled={s3Disabled}
                    fullWidth
                  />
                  <TextField
                    label={t("storageSettings.s3.fields.secretKey")}
                    value={s3Config.secretKey}
                    onChange={(event) =>
                      updateS3Config("secretKey", event.target.value)
                    }
                    disabled={s3Disabled}
                    type="password"
                    fullWidth
                  />
                </Box>

                <Box>
                  <Button
                    variant="contained"
                    onClick={() => void saveS3AndActivate()}
                    disabled={s3Disabled || s3Saving}
                    startIcon={
                      s3Saving ? (
                        <CircularProgress size={16} color="inherit" />
                      ) : (
                        <SaveIcon />
                      )
                    }
                  >
                    {t("settings.actions.save")}
                  </Button>
                </Box>
              </Stack>
            </SettingsSection>
          )}

          <SettingsSection
            title={t("settings.general.fields.storageSpaceMode")}
            description={t("settings.general.storageSpaceHelp.description")}
            status={storageSpaceModeStatus}
          >
            <ToggleButtonGroup
              size="small"
              exclusive
              value={storageSpaceMode}
              onChange={(_, next) =>
                void handleStorageSpaceModeChange(next as StorageSpaceMode | null)
              }
              disabled={storageSpaceDisabled}
              aria-label={t("settings.general.fields.storageSpaceMode")}
              fullWidth
              sx={{
                "& .MuiToggleButton-root": {
                  flex: 1,
                  minWidth: 0,
                  whiteSpace: "normal",
                  lineHeight: 1.2,
                },
              }}
            >
              {storageSpaceOptions.map((option) => (
                <ToggleButton key={option} value={option}>
                  {t(`settings.general.storageSpaceMode.${option}`)}
                </ToggleButton>
              ))}
            </ToggleButtonGroup>
          </SettingsSection>
        </Stack>
      </Paper>
      </Box>
    </Stack>
  );
};
