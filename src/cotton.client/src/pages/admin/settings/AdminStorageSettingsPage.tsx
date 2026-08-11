import SaveIcon from "@mui/icons-material/Save";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  MenuItem,
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
  type ChunkSizeSettings,
  type S3Config,
  type StoragePipelineSettings,
  type StorageSpaceMode,
  type StorageType,
} from "../../../shared/api/settingsApi";
import { showApiErrorToast } from "../../../shared/api/httpClient";
import {
  selectDeveloperSettingsUnlocked,
  useLocalPreferencesStore,
} from "../../../shared/store/localPreferencesStore";
import { SettingsSection } from "./SettingsSection";
import { storageSpaceOptions } from "./adminGeneralSettingsModel";
import type { SaveStatus } from "./useAutoSavedSetting";
import { AdminPageSurface } from "../components/AdminPageSurface";
import { AdminPageHeader } from "../components/AdminPageHeader";
import { SAVED_STATUS_VISIBLE_MS } from "./adminSettingSaveStatus";
import { DefaultUserStorageSettings } from "./DefaultUserStorageSettings";
import { SettingsSaveButton } from "./SettingsSaveButton";

type FlashTimers = {
  storageType: number | null;
  s3: number | null;
  storageSpace: number | null;
  chunkSize: number | null;
  pipeline: number | null;
};

const flashTimerKeys: Array<keyof FlashTimers> = [
  "storageType",
  "s3",
  "storageSpace",
  "chunkSize",
  "pipeline",
];

const bytesPerMiB = 1024 ** 2;
const defaultChunkSizeOptionsBytes = [4, 8, 16].map(
  (value) => value * bytesPerMiB,
);

const defaultStoragePipelineSettings: StoragePipelineSettings = {
  compressionLevel: 1,
  minCompressionLevel: 1,
  maxCompressionLevel: 22,
  cipherChunkSizeBytes: bytesPerMiB,
  minCipherChunkSizeBytes: 128 * 1024,
  maxCipherChunkSizeBytes: 64 * bytesPerMiB,
  supportedCipherChunkSizeBytes: [
    128 * 1024,
    bytesPerMiB,
    4 * bytesPerMiB,
    16 * bytesPerMiB,
  ],
  encryptionThreads: 1,
  minEncryptionThreads: 1,
  maxEncryptionThreads: 1,
  supportedEncryptionThreads: [1],
};

const formatChunkSize = (bytes: number): string => {
  const mib = bytes / bytesPerMiB;
  return `${Number(mib.toFixed(2)).toString()} MiB`;
};

const getSupportedChunkSizeOptions = (settings: ChunkSizeSettings): number[] =>
  settings.supportedMaxChunkSizeBytes.length > 0
    ? settings.supportedMaxChunkSizeBytes
    : defaultChunkSizeOptionsBytes;

const emptyS3Config: S3Config = {
  endpoint: "",
  region: "",
  bucket: "",
  accessKey: "",
  secretKey: "",
};

const clearFlashTimers = (flashTimers: FlashTimers): void => {
  for (const key of flashTimerKeys) {
    const pendingTimer = flashTimers[key];
    if (pendingTimer !== null) {
      window.clearTimeout(pendingTimer);
      flashTimers[key] = null;
    }
  }
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
  }, SAVED_STATUS_VISIBLE_MS);
};

const combineStatuses = (...statuses: SaveStatus[]): SaveStatus => {
  if (statuses.includes("saving")) return "saving";
  if (statuses.includes("error")) return "error";
  if (statuses.includes("loading")) return "loading";
  if (statuses.includes("saved")) return "saved";
  return "idle";
};

const isStatusBusy = (status: SaveStatus): boolean =>
  status === "loading" || status === "saving";

const isAnyStatusSaving = (...statuses: SaveStatus[]): boolean =>
  statuses.includes("saving");

const isStorageTypeDisabled = (
  loadFailed: boolean,
  storageTypeStatus: SaveStatus,
  s3Status: SaveStatus,
): boolean =>
  loadFailed || isStatusBusy(storageTypeStatus) || s3Status === "saving";

const isLoadedSettingDisabled = (
  loadFailed: boolean,
  status: SaveStatus,
): boolean => loadFailed || isStatusBusy(status);

export const AdminStorageSettingsPage = () => {
  const { t } = useTranslation("admin");
  const developerSettingsUnlocked = useLocalPreferencesStore(
    selectDeveloperSettingsUnlocked,
  );

  const [loadError, setLoadError] = useState<string | null>(null);
  const [loadFailed, setLoadFailed] = useState(false);

  const [storageType, setStorageType] = useState<StorageType>("Local");
  const [savedStorageType, setSavedStorageType] =
    useState<StorageType>("Local");
  const [storageTypeStatus, setStorageTypeStatus] =
    useState<SaveStatus>("loading");

  const [s3Config, setS3Config] = useState<S3Config>(emptyS3Config);
  const [s3Status, setS3Status] = useState<SaveStatus>("loading");

  const [storageSpaceMode, setStorageSpaceMode] =
    useState<StorageSpaceMode>("Optimal");
  const [savedStorageSpaceMode, setSavedStorageSpaceMode] =
    useState<StorageSpaceMode>("Optimal");
  const [storageSpaceModeStatus, setStorageSpaceModeStatus] =
    useState<SaveStatus>("loading");

  const [defaultUserQuotaBytes, setDefaultUserQuotaBytes] = useState<
    number | null | undefined
  >(undefined);
  const [defaultTemplateNodeId, setDefaultTemplateNodeId] = useState<
    string | null | undefined
  >(undefined);

  const [chunkSizeBytes, setChunkSizeBytes] = useState(
    defaultChunkSizeOptionsBytes[0],
  );
  const [savedChunkSizeBytes, setSavedChunkSizeBytes] = useState(
    defaultChunkSizeOptionsBytes[0],
  );
  const [supportedChunkSizeBytes, setSupportedChunkSizeBytes] = useState(
    defaultChunkSizeOptionsBytes,
  );
  const [chunkSizeStatus, setChunkSizeStatus] = useState<SaveStatus>("loading");

  const [storagePipelineSettings, setStoragePipelineSettings] =
    useState<StoragePipelineSettings>(defaultStoragePipelineSettings);
  const [savedStoragePipelineSettings, setSavedStoragePipelineSettings] =
    useState<StoragePipelineSettings>(defaultStoragePipelineSettings);
  const [compressionLevelInput, setCompressionLevelInput] = useState(
    defaultStoragePipelineSettings.compressionLevel.toString(),
  );
  const [storagePipelineStatus, setStoragePipelineStatus] =
    useState<SaveStatus>("loading");

  const flashTimers = useMemo<FlashTimers>(
    () => ({
      storageType: null,
      s3: null,
      storageSpace: null,
      chunkSize: null,
      pipeline: null,
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
      setChunkSizeStatus("loading");
      setStoragePipelineStatus("loading");

      try {
        const [
          nextStorageType,
          nextS3Config,
          nextStorageSpaceMode,
          nextDefaultUserQuotaBytes,
          nextDefaultTemplateNodeId,
          nextChunkSizeSettings,
          nextStoragePipelineSettings,
        ] = await Promise.all([
          settingsApi.getStorageType(),
          settingsApi.getS3Config(),
          settingsApi.getStorageSpaceMode(),
          settingsApi.getDefaultUserStorageQuotaBytes(),
          settingsApi.getDefaultUserTemplateNodeId(),
          settingsApi.getChunkSizeSettings(),
          settingsApi.getStoragePipelineSettings(),
        ]);

        if (!active) return;

        setStorageType(nextStorageType);
        setSavedStorageType(nextStorageType);
        setS3Config(nextS3Config);
        setStorageSpaceMode(nextStorageSpaceMode);
        setSavedStorageSpaceMode(nextStorageSpaceMode);
        setDefaultUserQuotaBytes(nextDefaultUserQuotaBytes);
        setDefaultTemplateNodeId(nextDefaultTemplateNodeId);
        setChunkSizeBytes(nextChunkSizeSettings.maxChunkSizeBytes);
        setSavedChunkSizeBytes(nextChunkSizeSettings.maxChunkSizeBytes);
        setSupportedChunkSizeBytes(
          getSupportedChunkSizeOptions(nextChunkSizeSettings),
        );
        setStoragePipelineSettings(nextStoragePipelineSettings);
        setSavedStoragePipelineSettings(nextStoragePipelineSettings);
        setCompressionLevelInput(
          nextStoragePipelineSettings.compressionLevel.toString(),
        );
        setLoadFailed(false);
        setStorageTypeStatus("idle");
        setS3Status("idle");
        setStorageSpaceModeStatus("idle");
        setChunkSizeStatus("idle");
        setStoragePipelineStatus("idle");
      } catch {
        if (!active) return;
        setLoadError(t("storageSettings.errors.loadFailed"));
        setLoadFailed(true);
        setStorageTypeStatus("error");
        setS3Status("error");
        setStorageSpaceModeStatus("error");
        setChunkSizeStatus("error");
        setStoragePipelineStatus("error");
      }
    };

    void load();

    return () => {
      active = false;
      clearFlashTimers(flashTimers);
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
      next === storageType ||
      storageTypeStatus === "loading" ||
      storageTypeStatus === "saving" ||
      s3Status === "saving"
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
      storageTypeStatus === "loading" ||
      storageTypeStatus === "saving" ||
      s3Status === "loading" ||
      s3Status === "saving"
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

  const handleChunkSizeChange = async (next: number | null) => {
    if (
      next === null ||
      next === chunkSizeBytes ||
      chunkSizeStatus === "loading" ||
      chunkSizeStatus === "saving" ||
      storagePipelineStatus === "loading" ||
      storagePipelineStatus === "saving"
    ) {
      return;
    }

    const previous = savedChunkSizeBytes;
    setChunkSizeBytes(next);
    setChunkSizeStatus("saving");

    try {
      const settings = await settingsApi.setChunkSize(next);
      setChunkSizeBytes(settings.maxChunkSizeBytes);
      setSavedChunkSizeBytes(settings.maxChunkSizeBytes);
      setSupportedChunkSizeBytes(getSupportedChunkSizeOptions(settings));
      flashStatus(setChunkSizeStatus, flashTimers, "chunkSize");
    } catch (error) {
      setChunkSizeBytes(previous);
      setChunkSizeStatus("error");
      showApiErrorToast(
        error,
        t("storageSettings.errors.chunkSizeSaveFailed"),
        "admin-storage-settings:chunk-size:save-failed",
      );
    }
  };

  const applyStoragePipelineSettings = (settings: StoragePipelineSettings) => {
    setStoragePipelineSettings(settings);
    setSavedStoragePipelineSettings(settings);
    setCompressionLevelInput(settings.compressionLevel.toString());
  };

  const handleCompressionLevelSave = async () => {
    if (
      storagePipelineStatus === "loading" ||
      storagePipelineStatus === "saving" ||
      chunkSizeStatus === "loading" ||
      chunkSizeStatus === "saving"
    ) {
      return;
    }

    const normalized = compressionLevelInput.trim();
    const next = Number(normalized);
    if (!Number.isInteger(next)) {
      setStoragePipelineStatus("error");
      return;
    }

    const previous = savedStoragePipelineSettings;
    setStoragePipelineStatus("saving");
    try {
      const settings = await settingsApi.setCompressionLevel(next);
      applyStoragePipelineSettings(settings);
      flashStatus(setStoragePipelineStatus, flashTimers, "pipeline");
    } catch (error) {
      setStoragePipelineSettings(previous);
      setCompressionLevelInput(previous.compressionLevel.toString());
      setStoragePipelineStatus("error");
      showApiErrorToast(
        error,
        t("storageSettings.errors.storagePipelineSaveFailed"),
        "admin-storage-settings:pipeline:compression-level-save-failed",
      );
    }
  };

  const handleCipherChunkSizeChange = async (next: number | null) => {
    if (
      next === null ||
      next === storagePipelineSettings.cipherChunkSizeBytes ||
      storagePipelineStatus === "loading" ||
      storagePipelineStatus === "saving" ||
      chunkSizeStatus === "loading" ||
      chunkSizeStatus === "saving"
    ) {
      return;
    }

    const previous = savedStoragePipelineSettings;
    setStoragePipelineSettings((current) => ({
      ...current,
      cipherChunkSizeBytes: next,
    }));
    setStoragePipelineStatus("saving");
    try {
      const settings = await settingsApi.setCipherChunkSize(next);
      applyStoragePipelineSettings(settings);
      flashStatus(setStoragePipelineStatus, flashTimers, "pipeline");
    } catch (error) {
      setStoragePipelineSettings(previous);
      setStoragePipelineStatus("error");
      showApiErrorToast(
        error,
        t("storageSettings.errors.storagePipelineSaveFailed"),
        "admin-storage-settings:pipeline:cipher-chunk-size-save-failed",
      );
    }
  };

  const handleEncryptionThreadsChange = async (next: number | null) => {
    if (
      next === null ||
      next === storagePipelineSettings.encryptionThreads ||
      storagePipelineStatus === "loading" ||
      storagePipelineStatus === "saving" ||
      chunkSizeStatus === "loading" ||
      chunkSizeStatus === "saving"
    ) {
      return;
    }

    const previous = savedStoragePipelineSettings;
    setStoragePipelineSettings((current) => ({
      ...current,
      encryptionThreads: next,
    }));
    setStoragePipelineStatus("saving");
    try {
      const settings = await settingsApi.setEncryptionThreads(next);
      applyStoragePipelineSettings(settings);
      flashStatus(setStoragePipelineStatus, flashTimers, "pipeline");
    } catch (error) {
      setStoragePipelineSettings(previous);
      setStoragePipelineStatus("error");
      showApiErrorToast(
        error,
        t("storageSettings.errors.storagePipelineSaveFailed"),
        "admin-storage-settings:pipeline:encryption-threads-save-failed",
      );
    }
  };

  const handleStorageSpaceModeChange = async (
    next: StorageSpaceMode | null,
  ) => {
    if (
      !next ||
      next === storageSpaceMode ||
      storageSpaceModeStatus === "loading" ||
      storageSpaceModeStatus === "saving"
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

  const storageTypeDisabled = isStorageTypeDisabled(
    loadFailed,
    storageTypeStatus,
    s3Status,
  );
  const s3Disabled = isLoadedSettingDisabled(loadFailed, s3Status);
  const s3Saving = isAnyStatusSaving(s3Status, storageTypeStatus);
  const storageSpaceDisabled = isLoadedSettingDisabled(
    loadFailed,
    storageSpaceModeStatus,
  );
  const storagePipelineGroupStatus = combineStatuses(
    chunkSizeStatus,
    storagePipelineStatus,
  );
  const storagePipelineGroupDisabled = isLoadedSettingDisabled(
    loadFailed,
    storagePipelineGroupStatus,
  );
  const chunkSizeDisabled = storagePipelineGroupDisabled;
  const storagePipelineDisabled = storagePipelineGroupDisabled;
  const compressionLevelChanged =
    compressionLevelInput.trim() !==
    savedStoragePipelineSettings.compressionLevel.toString();

  return (
    <Stack>
      <AdminPageSurface>
        <Stack p={3} spacing={3} divider={<Divider flexItem />}>
          <AdminPageHeader
            title={t("storageSettings.title")}
            description={t("storageSettings.description")}
          />

          {loadError && <Alert severity="error">{loadError}</Alert>}

          <SettingsSection
            title={t("storageSettings.fields.storageType")}
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
                void handleStorageSpaceModeChange(
                  next as StorageSpaceMode | null,
                )
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

          {developerSettingsUnlocked && (
            <SettingsSection
              title={t("storageSettings.pipeline.title")}
              description={t("storageSettings.pipeline.description")}
              status={storagePipelineGroupStatus}
            >
              <Stack spacing={2}>
                <Box>
                  <Typography variant="subtitle2" gutterBottom>
                    {t("storageSettings.chunkSize.title")}
                  </Typography>
                  <ToggleButtonGroup
                    size="small"
                    exclusive
                    value={chunkSizeBytes}
                    onChange={(_, next: number | null) =>
                      void handleChunkSizeChange(next)
                    }
                    disabled={chunkSizeDisabled}
                    aria-label={t("storageSettings.chunkSize.ariaLabel")}
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
                    {supportedChunkSizeBytes.map((option) => (
                      <ToggleButton key={option} value={option}>
                        {formatChunkSize(option)}
                      </ToggleButton>
                    ))}
                  </ToggleButtonGroup>
                </Box>

                <Stack
                  direction={{ xs: "column", sm: "row" }}
                  spacing={2}
                  alignItems={{ xs: "stretch", sm: "flex-start" }}
                >
                  <TextField
                    label={t(
                      "storageSettings.pipeline.fields.compressionLevel",
                    )}
                    value={compressionLevelInput}
                    onChange={(event) => {
                      setCompressionLevelInput(event.target.value);
                      if (storagePipelineStatus === "error") {
                        setStoragePipelineStatus("idle");
                      }
                    }}
                    disabled={storagePipelineDisabled}
                    error={storagePipelineStatus === "error"}
                    helperText={t("storageSettings.pipeline.compressionHelp", {
                      min: storagePipelineSettings.minCompressionLevel,
                      max: storagePipelineSettings.maxCompressionLevel,
                    })}
                    type="number"
                    inputProps={{
                      min: storagePipelineSettings.minCompressionLevel,
                      max: storagePipelineSettings.maxCompressionLevel,
                      step: 1,
                    }}
                    fullWidth
                  />
                  <SettingsSaveButton
                    changed={compressionLevelChanged}
                    disabled={storagePipelineDisabled}
                    label={t("settings.actions.save")}
                    onSave={() => void handleCompressionLevelSave()}
                    saving={storagePipelineStatus === "saving"}
                  />
                </Stack>

                <Box>
                  <Typography variant="subtitle2" gutterBottom>
                    {t("storageSettings.pipeline.fields.cipherChunkSize")}
                  </Typography>
                  <ToggleButtonGroup
                    size="small"
                    exclusive
                    value={storagePipelineSettings.cipherChunkSizeBytes}
                    onChange={(_, next: number | null) =>
                      void handleCipherChunkSizeChange(next)
                    }
                    disabled={storagePipelineDisabled}
                    aria-label={t(
                      "storageSettings.pipeline.fields.cipherChunkSize",
                    )}
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
                    {storagePipelineSettings.supportedCipherChunkSizeBytes.map(
                      (option) => (
                        <ToggleButton key={option} value={option}>
                          {formatChunkSize(option)}
                        </ToggleButton>
                      ),
                    )}
                  </ToggleButtonGroup>
                </Box>

                <Box>
                  <Typography variant="subtitle2" gutterBottom>
                    {t("storageSettings.pipeline.fields.encryptionThreads")}
                  </Typography>
                  <ToggleButtonGroup
                    size="small"
                    exclusive
                    value={storagePipelineSettings.encryptionThreads}
                    onChange={(_, next: number | null) =>
                      void handleEncryptionThreadsChange(next)
                    }
                    disabled={storagePipelineDisabled}
                    aria-label={t(
                      "storageSettings.pipeline.fields.encryptionThreads",
                    )}
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
                    {storagePipelineSettings.supportedEncryptionThreads.map(
                      (option) => (
                        <ToggleButton key={option} value={option}>
                          {option.toString()}
                        </ToggleButton>
                      ),
                    )}
                  </ToggleButtonGroup>
                </Box>
              </Stack>
            </SettingsSection>
          )}

          <DefaultUserStorageSettings
            defaultUserQuotaBytes={defaultUserQuotaBytes}
            defaultTemplateNodeId={defaultTemplateNodeId}
            loadFailed={loadFailed}
          />
        </Stack>
      </AdminPageSurface>
    </Stack>
  );
};
