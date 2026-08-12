import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  settingsApi,
  type ChunkSizeSettings,
  type StoragePipelineSettings,
} from "@shared/api/settingsApi";
import { showApiErrorToast } from "@shared/api/httpClient";
import type { SaveStatus } from "./useAutoSavedSetting";
import { useSaveStatus } from "./useSaveStatus";

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

const getSupportedChunkSizeOptions = (settings: ChunkSizeSettings): number[] =>
  settings.supportedMaxChunkSizeBytes.length > 0
    ? settings.supportedMaxChunkSizeBytes
    : defaultChunkSizeOptionsBytes;

const combineStatuses = (...statuses: SaveStatus[]): SaveStatus => {
  if (statuses.includes("saving")) return "saving";
  if (statuses.includes("error")) return "error";
  if (statuses.includes("loading")) return "loading";
  if (statuses.includes("saved")) return "saved";
  return "idle";
};

export const useStoragePipelineSettings = () => {
  const { t } = useTranslation("admin");
  const [chunkSizeBytes, setChunkSizeBytes] = useState(
    defaultChunkSizeOptionsBytes[0],
  );
  const [savedChunkSizeBytes, setSavedChunkSizeBytes] = useState(
    defaultChunkSizeOptionsBytes[0],
  );
  const [supportedChunkSizeBytes, setSupportedChunkSizeBytes] = useState(
    defaultChunkSizeOptionsBytes,
  );
  const [settings, setSettings] = useState<StoragePipelineSettings>(
    defaultStoragePipelineSettings,
  );
  const [savedSettings, setSavedSettings] = useState<StoragePipelineSettings>(
    defaultStoragePipelineSettings,
  );
  const [compressionLevelInput, setCompressionLevelInput] = useState(
    defaultStoragePipelineSettings.compressionLevel.toString(),
  );
  const [loadFailed, setLoadFailed] = useState(false);
  const {
    markSaved: markChunkSizeSaved,
    setStatus: setChunkSizeStatus,
    status: chunkSizeStatus,
  } = useSaveStatus();
  const {
    markSaved: markPipelineSaved,
    setStatus: setPipelineStatus,
    status: pipelineStatus,
  } = useSaveStatus();

  const applySettings = useCallback((next: StoragePipelineSettings) => {
    setSettings(next);
    setSavedSettings(next);
    setCompressionLevelInput(next.compressionLevel.toString());
  }, []);

  const initialize = useCallback(
    (
      nextChunkSizeSettings: ChunkSizeSettings,
      nextPipelineSettings: StoragePipelineSettings,
    ) => {
      setChunkSizeBytes(nextChunkSizeSettings.maxChunkSizeBytes);
      setSavedChunkSizeBytes(nextChunkSizeSettings.maxChunkSizeBytes);
      setSupportedChunkSizeBytes(
        getSupportedChunkSizeOptions(nextChunkSizeSettings),
      );
      applySettings(nextPipelineSettings);
      setLoadFailed(false);
      setChunkSizeStatus("idle");
      setPipelineStatus("idle");
    },
    [applySettings, setChunkSizeStatus, setPipelineStatus],
  );

  const beginLoad = useCallback(() => {
    setChunkSizeStatus("loading");
    setPipelineStatus("loading");
  }, [setChunkSizeStatus, setPipelineStatus]);

  const failLoad = useCallback(() => {
    setLoadFailed(true);
    setChunkSizeStatus("error");
    setPipelineStatus("error");
  }, [setChunkSizeStatus, setPipelineStatus]);

  const isBusy =
    chunkSizeStatus === "loading" ||
    chunkSizeStatus === "saving" ||
    pipelineStatus === "loading" ||
    pipelineStatus === "saving";
  const disabled = loadFailed || isBusy;

  const handleChunkSizeChange = useCallback(
    async (next: number | null) => {
      if (next === null || next === chunkSizeBytes || isBusy) {
        return;
      }

      const previous = savedChunkSizeBytes;
      setChunkSizeBytes(next);
      setChunkSizeStatus("saving");

      try {
        const nextSettings = await settingsApi.setChunkSize(next);
        setChunkSizeBytes(nextSettings.maxChunkSizeBytes);
        setSavedChunkSizeBytes(nextSettings.maxChunkSizeBytes);
        setSupportedChunkSizeBytes(
          getSupportedChunkSizeOptions(nextSettings),
        );
        markChunkSizeSaved();
      } catch (error) {
        setChunkSizeBytes(previous);
        setChunkSizeStatus("error");
        showApiErrorToast(
          error,
          t("storageSettings.errors.chunkSizeSaveFailed"),
          "admin-storage-settings:chunk-size:save-failed",
        );
      }
    },
    [
      chunkSizeBytes,
      isBusy,
      markChunkSizeSaved,
      savedChunkSizeBytes,
      setChunkSizeStatus,
      t,
    ],
  );

  const handleCompressionLevelChange = useCallback(
    (value: string) => {
      setCompressionLevelInput(value);
      if (pipelineStatus === "error") {
        setPipelineStatus("idle");
      }
    },
    [pipelineStatus, setPipelineStatus],
  );

  const handleCompressionLevelSave = useCallback(async () => {
    if (isBusy) {
      return;
    }

    const next = Number(compressionLevelInput.trim());
    if (!Number.isInteger(next)) {
      setPipelineStatus("error");
      return;
    }

    const previous = savedSettings;
    setPipelineStatus("saving");
    try {
      const nextSettings = await settingsApi.setCompressionLevel(next);
      applySettings(nextSettings);
      markPipelineSaved();
    } catch (error) {
      setSettings(previous);
      setCompressionLevelInput(previous.compressionLevel.toString());
      setPipelineStatus("error");
      showApiErrorToast(
        error,
        t("storageSettings.errors.storagePipelineSaveFailed"),
        "admin-storage-settings:pipeline:compression-level-save-failed",
      );
    }
  }, [
    applySettings,
    compressionLevelInput,
    isBusy,
    markPipelineSaved,
    savedSettings,
    setPipelineStatus,
    t,
  ]);

  const handleCipherChunkSizeChange = useCallback(
    async (next: number | null) => {
      if (next === null || next === settings.cipherChunkSizeBytes || isBusy) {
        return;
      }

      const previous = savedSettings;
      setSettings((current) => ({ ...current, cipherChunkSizeBytes: next }));
      setPipelineStatus("saving");
      try {
        const nextSettings = await settingsApi.setCipherChunkSize(next);
        applySettings(nextSettings);
        markPipelineSaved();
      } catch (error) {
        setSettings(previous);
        setPipelineStatus("error");
        showApiErrorToast(
          error,
          t("storageSettings.errors.storagePipelineSaveFailed"),
          "admin-storage-settings:pipeline:cipher-chunk-size-save-failed",
        );
      }
    },
    [
      applySettings,
      isBusy,
      markPipelineSaved,
      savedSettings,
      setPipelineStatus,
      settings,
      t,
    ],
  );

  const handleEncryptionThreadsChange = useCallback(
    async (next: number | null) => {
      if (next === null || next === settings.encryptionThreads || isBusy) {
        return;
      }

      const previous = savedSettings;
      setSettings((current) => ({ ...current, encryptionThreads: next }));
      setPipelineStatus("saving");
      try {
        const nextSettings = await settingsApi.setEncryptionThreads(next);
        applySettings(nextSettings);
        markPipelineSaved();
      } catch (error) {
        setSettings(previous);
        setPipelineStatus("error");
        showApiErrorToast(
          error,
          t("storageSettings.errors.storagePipelineSaveFailed"),
          "admin-storage-settings:pipeline:encryption-threads-save-failed",
        );
      }
    },
    [
      applySettings,
      isBusy,
      markPipelineSaved,
      savedSettings,
      setPipelineStatus,
      settings,
      t,
    ],
  );

  return {
    beginLoad,
    chunkSizeBytes,
    compressionLevelChanged:
      compressionLevelInput.trim() !== savedSettings.compressionLevel.toString(),
    compressionLevelInput,
    disabled,
    failLoad,
    handleChunkSizeChange,
    handleCipherChunkSizeChange,
    handleCompressionLevelChange,
    handleCompressionLevelSave,
    handleEncryptionThreadsChange,
    initialize,
    pipelineStatus,
    sectionStatus: combineStatuses(chunkSizeStatus, pipelineStatus),
    settings,
    supportedChunkSizeBytes,
  };
};
