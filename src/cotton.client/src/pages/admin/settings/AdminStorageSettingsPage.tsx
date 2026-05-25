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
import { isGuidString } from "../../../shared/utils/guid";
import { storageSpaceOptions } from "./adminGeneralSettingsModel";
import type { SaveStatus } from "./useAutoSavedSetting";
import { AdminPageSurface } from "../components/AdminPageSurface";
import { SAVED_STATUS_VISIBLE_MS } from "./adminSettingSaveStatus";

type FlashTimers = {
  storageType: number | null;
  s3: number | null;
  storageSpace: number | null;
  quota: number | null;
  template: number | null;
  chunkSize: number | null;
  pipeline: number | null;
};

const bytesPerGiB = 1024 ** 3;
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
  supportedCipherChunkSizeBytes: [128 * 1024, bytesPerMiB, 4 * bytesPerMiB, 16 * bytesPerMiB],
  encryptionThreads: 1,
  minEncryptionThreads: 1,
  maxEncryptionThreads: 1,
  supportedEncryptionThreads: [1],
};

const formatQuotaInput = (quotaBytes: number | null): string => {
  if (!quotaBytes || quotaBytes <= 0) {
    return "";
  }

  return Number((quotaBytes / bytesPerGiB).toFixed(3)).toString();
};

const parseQuotaInput = (input: string): number | null => {
  const normalized = input.trim().replace(",", ".");
  if (normalized.length === 0) {
    return null;
  }

  const value = Number(normalized);
  if (!Number.isFinite(value) || value < 0) {
    throw new Error("invalid-quota");
  }

  if (value === 0) {
    return null;
  }

  return Math.round(value * bytesPerGiB);
};

const parseTemplateNodeIdInput = (input: string): string | null => {
  const trimmed = input.trim();
  if (trimmed.length === 0) {
    return null;
  }

  if (!isGuidString(trimmed)) {
    throw new Error("invalid-template-node-id");
  }

  return trimmed.toLowerCase();
};

const formatChunkSize = (bytes: number): string => {
  const mib = bytes / bytesPerMiB;
  return `${Number(mib.toFixed(2)).toString()} MiB`;
};

const getSupportedChunkSizeOptions = (
  settings: ChunkSizeSettings,
): number[] =>
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

export const AdminStorageSettingsPage = () => {
  const { t } = useTranslation("admin");
  const developerSettingsUnlocked = useLocalPreferencesStore(
    selectDeveloperSettingsUnlocked,
  );

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

  const [defaultUserQuotaGiB, setDefaultUserQuotaGiB] = useState("");
  const [savedDefaultUserQuotaGiB, setSavedDefaultUserQuotaGiB] = useState("");
  const [defaultUserQuotaStatus, setDefaultUserQuotaStatus] =
    useState<SaveStatus>("loading");

  const [defaultTemplateNodeId, setDefaultTemplateNodeId] = useState("");
  const [savedDefaultTemplateNodeId, setSavedDefaultTemplateNodeId] =
    useState("");
  const [defaultTemplateStatus, setDefaultTemplateStatus] =
    useState<SaveStatus>("loading");

  const [chunkSizeBytes, setChunkSizeBytes] = useState(
    defaultChunkSizeOptionsBytes[0],
  );
  const [savedChunkSizeBytes, setSavedChunkSizeBytes] = useState(
    defaultChunkSizeOptionsBytes[0],
  );
  const [supportedChunkSizeBytes, setSupportedChunkSizeBytes] = useState(
    defaultChunkSizeOptionsBytes,
  );
  const [chunkSizeStatus, setChunkSizeStatus] =
    useState<SaveStatus>("loading");

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
      quota: null,
      template: null,
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
      setDefaultUserQuotaStatus("loading");
      setDefaultTemplateStatus("loading");
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
        const quotaInput = formatQuotaInput(nextDefaultUserQuotaBytes);
        setDefaultUserQuotaGiB(quotaInput);
        setSavedDefaultUserQuotaGiB(quotaInput);
        setDefaultTemplateNodeId(nextDefaultTemplateNodeId ?? "");
        setSavedDefaultTemplateNodeId(nextDefaultTemplateNodeId ?? "");
        setChunkSizeBytes(nextChunkSizeSettings.maxChunkSizeBytes);
        setSavedChunkSizeBytes(nextChunkSizeSettings.maxChunkSizeBytes);
        setSupportedChunkSizeBytes(
          getSupportedChunkSizeOptions(nextChunkSizeSettings),
        );
        setStoragePipelineSettings(nextStoragePipelineSettings);
        setSavedStoragePipelineSettings(nextStoragePipelineSettings);
        setCompressionLevelInput(nextStoragePipelineSettings.compressionLevel.toString());
        setStorageTypeStatus("idle");
        setS3Status("idle");
        setStorageSpaceModeStatus("idle");
        setDefaultUserQuotaStatus("idle");
        setDefaultTemplateStatus("idle");
        setChunkSizeStatus("idle");
        setStoragePipelineStatus("idle");
      } catch {
        if (!active) return;
        setLoadError(t("storageSettings.errors.loadFailed"));
        setStorageTypeStatus("idle");
        setS3Status("idle");
        setStorageSpaceModeStatus("idle");
        setDefaultUserQuotaStatus("idle");
        setDefaultTemplateStatus("idle");
        setChunkSizeStatus("idle");
        setStoragePipelineStatus("idle");
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
      if (flashTimers.quota !== null) {
        window.clearTimeout(flashTimers.quota);
        flashTimers.quota = null;
      }
      if (flashTimers.template !== null) {
        window.clearTimeout(flashTimers.template);
        flashTimers.template = null;
      }
      if (flashTimers.chunkSize !== null) {
        window.clearTimeout(flashTimers.chunkSize);
        flashTimers.chunkSize = null;
      }
      if (flashTimers.pipeline !== null) {
        window.clearTimeout(flashTimers.pipeline);
        flashTimers.pipeline = null;
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

  const saveDefaultUserQuota = async () => {
    if (
      defaultUserQuotaStatus === "loading"
      || defaultUserQuotaStatus === "saving"
    ) {
      return;
    }

    let quotaBytes: number | null;
    try {
      quotaBytes = parseQuotaInput(defaultUserQuotaGiB);
    } catch {
      setDefaultUserQuotaStatus("error");
      return;
    }

    const previous = savedDefaultUserQuotaGiB;
    setDefaultUserQuotaStatus("saving");

    try {
      await settingsApi.setDefaultUserStorageQuotaBytes(quotaBytes);
      const saved = formatQuotaInput(quotaBytes);
      setDefaultUserQuotaGiB(saved);
      setSavedDefaultUserQuotaGiB(saved);
      flashStatus(setDefaultUserQuotaStatus, flashTimers, "quota");
    } catch (error) {
      setDefaultUserQuotaGiB(previous);
      setDefaultUserQuotaStatus("error");
      showApiErrorToast(
        error,
        t("storageSettings.errors.quotaSaveFailed"),
        "admin-storage-settings:quota:save-failed",
      );
    }
  };

  const saveDefaultTemplateNode = async () => {
    if (
      defaultTemplateStatus === "loading"
      || defaultTemplateStatus === "saving"
    ) {
      return;
    }

    let nodeId: string | null;
    try {
      nodeId = parseTemplateNodeIdInput(defaultTemplateNodeId);
    } catch {
      setDefaultTemplateStatus("error");
      return;
    }

    const previous = savedDefaultTemplateNodeId;
    setDefaultTemplateStatus("saving");

    try {
      await settingsApi.setDefaultUserTemplateNodeId(nodeId);
      const saved = nodeId ?? "";
      setDefaultTemplateNodeId(saved);
      setSavedDefaultTemplateNodeId(saved);
      flashStatus(setDefaultTemplateStatus, flashTimers, "template");
    } catch (error) {
      setDefaultTemplateNodeId(previous);
      setDefaultTemplateStatus("error");
      showApiErrorToast(
        error,
        t("storageSettings.errors.templateSaveFailed"),
        "admin-storage-settings:template:save-failed",
      );
    }
  };

  const handleChunkSizeChange = async (next: number | null) => {
    if (
      next === null
      || next === chunkSizeBytes
      || chunkSizeStatus === "loading"
      || chunkSizeStatus === "saving"
      || storagePipelineStatus === "loading"
      || storagePipelineStatus === "saving"
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
      storagePipelineStatus === "loading"
      || storagePipelineStatus === "saving"
      || chunkSizeStatus === "loading"
      || chunkSizeStatus === "saving"
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
      next === null
      || next === storagePipelineSettings.cipherChunkSizeBytes
      || storagePipelineStatus === "loading"
      || storagePipelineStatus === "saving"
      || chunkSizeStatus === "loading"
      || chunkSizeStatus === "saving"
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
      next === null
      || next === storagePipelineSettings.encryptionThreads
      || storagePipelineStatus === "loading"
      || storagePipelineStatus === "saving"
      || chunkSizeStatus === "loading"
      || chunkSizeStatus === "saving"
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
  const storagePipelineGroupStatus = combineStatuses(
    chunkSizeStatus,
    storagePipelineStatus,
  );
  const storagePipelineGroupDisabled =
    storagePipelineGroupStatus === "loading"
    || storagePipelineGroupStatus === "saving";
  const chunkSizeDisabled = storagePipelineGroupDisabled;
  const storagePipelineDisabled = storagePipelineGroupDisabled;
  const compressionLevelChanged =
    compressionLevelInput.trim() !== savedStoragePipelineSettings.compressionLevel.toString();
  const quotaSaving = defaultUserQuotaStatus === "saving";
  const quotaDisabled =
    defaultUserQuotaStatus === "loading" || defaultUserQuotaStatus === "saving";
  const quotaChanged = defaultUserQuotaGiB !== savedDefaultUserQuotaGiB;
  const templateSaving = defaultTemplateStatus === "saving";
  const templateDisabled =
    defaultTemplateStatus === "loading" || defaultTemplateStatus === "saving";
  const templateChanged = defaultTemplateNodeId !== savedDefaultTemplateNodeId;

  return (
    <Stack>
      <AdminPageSurface>
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
                  label={t("storageSettings.pipeline.fields.compressionLevel")}
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
                <Button
                  variant="contained"
                  onClick={() => void handleCompressionLevelSave()}
                  disabled={storagePipelineDisabled || !compressionLevelChanged}
                  startIcon={
                    storagePipelineStatus === "saving" ? (
                      <CircularProgress size={16} color="inherit" />
                    ) : (
                      <SaveIcon />
                    )
                  }
                  sx={{ minWidth: { xs: "100%", sm: 120 } }}
                >
                  {t("settings.actions.save")}
                </Button>
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
                  aria-label={t("storageSettings.pipeline.fields.cipherChunkSize")}
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
                  {storagePipelineSettings.supportedCipherChunkSizeBytes.map((option) => (
                    <ToggleButton key={option} value={option}>
                      {formatChunkSize(option)}
                    </ToggleButton>
                  ))}
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
                  aria-label={t("storageSettings.pipeline.fields.encryptionThreads")}
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
                  {storagePipelineSettings.supportedEncryptionThreads.map((option) => (
                    <ToggleButton key={option} value={option}>
                      {option.toString()}
                    </ToggleButton>
                  ))}
                </ToggleButtonGroup>
              </Box>
              </Stack>
            </SettingsSection>
          )}

          <SettingsSection
            title={t("storageSettings.quota.title")}
            description={t("storageSettings.quota.description")}
            status={defaultUserQuotaStatus}
          >
            <Stack
              direction={{ xs: "column", sm: "row" }}
              spacing={2}
              alignItems={{ xs: "stretch", sm: "flex-start" }}
            >
              <TextField
                label={t("storageSettings.quota.fields.defaultUserQuotaGiB")}
                value={defaultUserQuotaGiB}
                onChange={(event) => {
                  setDefaultUserQuotaGiB(event.target.value);
                  if (defaultUserQuotaStatus === "error") {
                    setDefaultUserQuotaStatus("idle");
                  }
                }}
                disabled={quotaDisabled}
                error={defaultUserQuotaStatus === "error"}
                helperText={t("storageSettings.quota.help")}
                type="number"
                inputProps={{ min: 0, step: 0.25 }}
                fullWidth
              />
              <Button
                variant="contained"
                onClick={() => void saveDefaultUserQuota()}
                disabled={quotaDisabled || !quotaChanged}
                startIcon={
                  quotaSaving ? (
                    <CircularProgress size={16} color="inherit" />
                  ) : (
                    <SaveIcon />
                  )
                }
                sx={{ minWidth: { xs: "100%", sm: 120 } }}
              >
                {t("settings.actions.save")}
              </Button>
            </Stack>
          </SettingsSection>

          <SettingsSection
            title={t("storageSettings.template.title")}
            description={t("storageSettings.template.description")}
            status={defaultTemplateStatus}
          >
            <Stack
              direction={{ xs: "column", sm: "row" }}
              spacing={2}
              alignItems={{ xs: "stretch", sm: "flex-start" }}
            >
              <TextField
                label={t("storageSettings.template.fields.nodeId")}
                value={defaultTemplateNodeId}
                onChange={(event) => {
                  setDefaultTemplateNodeId(event.target.value);
                  if (defaultTemplateStatus === "error") {
                    setDefaultTemplateStatus("idle");
                  }
                }}
                disabled={templateDisabled}
                error={defaultTemplateStatus === "error"}
                helperText={t("storageSettings.template.help")}
                fullWidth
              />
              <Button
                variant="contained"
                onClick={() => void saveDefaultTemplateNode()}
                disabled={templateDisabled || !templateChanged}
                startIcon={
                  templateSaving ? (
                    <CircularProgress size={16} color="inherit" />
                  ) : (
                    <SaveIcon />
                  )
                }
                sx={{ minWidth: { xs: "100%", sm: 120 } }}
              >
                {t("settings.actions.save")}
              </Button>
            </Stack>
          </SettingsSection>
        </Stack>
      </AdminPageSurface>
    </Stack>
  );
};
