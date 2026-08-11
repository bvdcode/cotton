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
  const chunkSizeSave = useSaveStatus();
  const pipelineSave = useSaveStatus();

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
      chunkSizeSave.setStatus("idle");
      pipelineSave.setStatus("idle");
    },
    [applySettings, chunkSizeSave.setStatus, pipelineSave.setStatus],
  );

  const beginLoad = useCallback(() => {
    chunkSizeSave.setStatus("loading");
    pipelineSave.setStatus("loading");
  }, [chunkSizeSave.setStatus, pipelineSave.setStatus]);

  const failLoad = useCallback(() => {
    setLoadFailed(true);
    chunkSizeSave.setStatus("error");
    pipelineSave.setStatus("error");
  }, [chunkSizeSave.setStatus, pipelineSave.setStatus]);

  const isBusy =
    chunkSizeSave.status === "loading" ||
    chunkSizeSave.status === "saving" ||
    pipelineSave.status === "loading" ||
    pipelineSave.status === "saving";
  const disabled = loadFailed || isBusy;

  const handleChunkSizeChange = useCallback(
    async (next: number | null) => {
      if (next === null || next === chunkSizeBytes || isBusy) {
        return;
      }

      const previous = savedChunkSizeBytes;
      setChunkSizeBytes(next);
      chunkSizeSave.setStatus("saving");

      try {
        const nextSettings = await settingsApi.setChunkSize(next);
        setChunkSizeBytes(nextSettings.maxChunkSizeBytes);
        setSavedChunkSizeBytes(nextSettings.maxChunkSizeBytes);
        setSupportedChunkSizeBytes(
          getSupportedChunkSizeOptions(nextSettings),
        );
        chunkSizeSave.markSaved();
      } catch (error) {
        setChunkSizeBytes(previous);
        chunkSizeSave.setStatus("error");
        showApiErrorToast(
          error,
          t("storageSettings.errors.chunkSizeSaveFailed"),
          "admin-storage-settings:chunk-size:save-failed",
        );
      }
    }, [chunkSizeBytes, chunkSizeSave, isBusy, savedChunkSizeBytes, t],
  );

  const handleCompressionLevelChange = useCallback(
    (value: string) => {
      setCompressionLevelInput(value);
      if (pipelineSave.status === "error") {
        pipelineSave.setStatus("idle");
      }
    },
    [pipelineSave.setStatus, pipelineSave.status],
  );

  const handleCompressionLevelSave = useCallback(async () => {
    if (isBusy) {
      return;
    }

    const next = Number(compressionLevelInput.trim());
    if (!Number.isInteger(next)) {
      pipelineSave.setStatus("error");
      return;
    }

    const previous = savedSettings;
    pipelineSave.setStatus("saving");
    try {
      const nextSettings = await settingsApi.setCompressionLevel(next);
      applySettings(nextSettings);
      pipelineSave.markSaved();
    } catch (error) {
      setSettings(previous);
      setCompressionLevelInput(previous.compressionLevel.toString());
      pipelineSave.setStatus("error");
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
    pipelineSave,
    savedSettings,
    t,
  ]);

  const handleCipherChunkSizeChange = useCallback(
    async (next: number | null) => {
      if (next === null || next === settings.cipherChunkSizeBytes || isBusy) {
        return;
      }

      const previous = savedSettings;
      setSettings((current) => ({ ...current, cipherChunkSizeBytes: next }));
      pipelineSave.setStatus("saving");
      try {
        const nextSettings = await settingsApi.setCipherChunkSize(next);
        applySettings(nextSettings);
        pipelineSave.markSaved();
      } catch (error) {
        setSettings(previous);
        pipelineSave.setStatus("error");
        showApiErrorToast(
          error,
          t("storageSettings.errors.storagePipelineSaveFailed"),
          "admin-storage-settings:pipeline:cipher-chunk-size-save-failed",
        );
      }
    }, [applySettings, isBusy, pipelineSave, savedSettings, settings, t],
  );

  const handleEncryptionThreadsChange = useCallback(
    async (next: number | null) => {
      if (next === null || next === settings.encryptionThreads || isBusy) {
        return;
      }

      const previous = savedSettings;
      setSettings((current) => ({ ...current, encryptionThreads: next }));
      pipelineSave.setStatus("saving");
      try {
        const nextSettings = await settingsApi.setEncryptionThreads(next);
        applySettings(nextSettings);
        pipelineSave.markSaved();
      } catch (error) {
        setSettings(previous);
        pipelineSave.setStatus("error");
        showApiErrorToast(
          error,
          t("storageSettings.errors.storagePipelineSaveFailed"),
          "admin-storage-settings:pipeline:encryption-threads-save-failed",
        );
      }
    }, [applySettings, isBusy, pipelineSave, savedSettings, settings, t],
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
    pipelineStatus: pipelineSave.status,
    sectionStatus: combineStatuses(chunkSizeSave.status, pipelineSave.status),
    settings,
    supportedChunkSizeBytes,
  };
};
