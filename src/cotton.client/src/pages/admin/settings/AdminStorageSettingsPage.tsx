import { Alert, Divider, Stack } from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { settingsApi } from "../../../shared/api/settingsApi";
import {
  selectDeveloperSettingsUnlocked,
  useLocalPreferencesStore,
} from "../../../shared/store/localPreferencesStore";
import { AdminPageSurface } from "../components/AdminPageSurface";
import { AdminPageHeader } from "../components/AdminPageHeader";
import { DefaultUserStorageSettings } from "./DefaultUserStorageSettings";
import { StorageBackendSettings } from "./StorageBackendSettings";
import { StoragePipelineSettingsSection } from "./StoragePipelineSettingsSection";
import { StorageSpaceModeSetting } from "./StorageSpaceModeSetting";
import { useStorageBackendSettings } from "./useStorageBackendSettings";
import { useStoragePipelineSettings } from "./useStoragePipelineSettings";
import { useStorageSpaceModeSetting } from "./useStorageSpaceModeSetting";

export const AdminStorageSettingsPage = () => {
  const { t } = useTranslation("admin");
  const developerSettingsUnlocked = useLocalPreferencesStore(
    selectDeveloperSettingsUnlocked,
  );

  const [loadError, setLoadError] = useState<string | null>(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const storageBackend = useStorageBackendSettings();
  const storageSpaceMode = useStorageSpaceModeSetting();
  const storagePipeline = useStoragePipelineSettings();

  const [defaultUserQuotaBytes, setDefaultUserQuotaBytes] = useState<
    number | null | undefined
  >(undefined);
  const [defaultTemplateNodeId, setDefaultTemplateNodeId] = useState<
    string | null | undefined
  >(undefined);

  useEffect(() => {
    let active = true;

    const load = async () => {
      setLoadError(null);
      storageBackend.beginLoad();
      storageSpaceMode.beginLoad();
      storagePipeline.beginLoad();

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

        storageBackend.initialize(nextStorageType, nextS3Config);
        storageSpaceMode.initialize(nextStorageSpaceMode);
        storagePipeline.initialize(
          nextChunkSizeSettings,
          nextStoragePipelineSettings,
        );
        setDefaultUserQuotaBytes(nextDefaultUserQuotaBytes);
        setDefaultTemplateNodeId(nextDefaultTemplateNodeId);
        setLoadFailed(false);
      } catch {
        if (!active) return;
        setLoadError(t("storageSettings.errors.loadFailed"));
        setLoadFailed(true);
        storageBackend.failLoad();
        storageSpaceMode.failLoad();
        storagePipeline.failLoad();
      }
    };

    void load();

    return () => {
      active = false;
    };
  }, [
    storageBackend.beginLoad,
    storageBackend.failLoad,
    storageBackend.initialize,
    storagePipeline.beginLoad,
    storagePipeline.failLoad,
    storagePipeline.initialize,
    storageSpaceMode.beginLoad,
    storageSpaceMode.failLoad,
    storageSpaceMode.initialize,
    t,
  ]);

  return (
    <Stack>
      <AdminPageSurface>
        <Stack p={3} spacing={3} divider={<Divider flexItem />}>
          <AdminPageHeader
            title={t("storageSettings.title")}
            description={t("storageSettings.description")}
          />

          {loadError && <Alert severity="error">{loadError}</Alert>}

          <StorageBackendSettings
            onS3Change={storageBackend.setS3Config}
            onSaveS3={() => void storageBackend.saveS3AndActivate()}
            onStorageTypeChange={(next) =>
              void storageBackend.handleStorageTypeChange(next)
            }
            s3Config={storageBackend.s3Config}
            s3Disabled={storageBackend.s3Disabled}
            s3Saving={storageBackend.s3Saving}
            s3Status={storageBackend.s3Status}
            storageType={storageBackend.storageType}
            storageTypeDisabled={storageBackend.storageTypeDisabled}
            storageTypeStatus={storageBackend.storageTypeStatus}
          />

          <StorageSpaceModeSetting
            disabled={storageSpaceMode.disabled}
            onChange={(next) => void storageSpaceMode.handleChange(next)}
            status={storageSpaceMode.status}
            value={storageSpaceMode.value}
          />

          {developerSettingsUnlocked && (
            <StoragePipelineSettingsSection
              chunkSizeBytes={storagePipeline.chunkSizeBytes}
              compressionLevelChanged={
                storagePipeline.compressionLevelChanged
              }
              compressionLevelInput={storagePipeline.compressionLevelInput}
              disabled={storagePipeline.disabled}
              onChunkSizeChange={(next) =>
                void storagePipeline.handleChunkSizeChange(next)
              }
              onCipherChunkSizeChange={(next) =>
                void storagePipeline.handleCipherChunkSizeChange(next)
              }
              onCompressionLevelChange={
                storagePipeline.handleCompressionLevelChange
              }
              onCompressionLevelSave={() =>
                void storagePipeline.handleCompressionLevelSave()
              }
              onEncryptionThreadsChange={(next) =>
                void storagePipeline.handleEncryptionThreadsChange(next)
              }
              pipelineStatus={storagePipeline.pipelineStatus}
              settings={storagePipeline.settings}
              sectionStatus={storagePipeline.sectionStatus}
              supportedChunkSizeBytes={
                storagePipeline.supportedChunkSizeBytes
              }
            />
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
