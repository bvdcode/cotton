import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  settingsApi,
  type S3Config,
  type StorageType,
} from "@shared/api/settingsApi";
import { showApiErrorToast } from "@shared/api/httpClient";
import { useSaveStatus } from "./useSaveStatus";

const emptyS3Config: S3Config = {
  endpoint: "",
  region: "",
  bucket: "",
  accessKey: "",
  secretKey: "",
};

export const useStorageBackendSettings = () => {
  const { t } = useTranslation("admin");
  const [storageType, setStorageType] = useState<StorageType>("Local");
  const [savedStorageType, setSavedStorageType] =
    useState<StorageType>("Local");
  const [s3Config, setS3Config] = useState<S3Config>(emptyS3Config);
  const [loadFailed, setLoadFailed] = useState(false);
  const storageTypeSave = useSaveStatus();
  const s3Save = useSaveStatus();

  const initialize = useCallback(
    (nextStorageType: StorageType, nextS3Config: S3Config) => {
      setStorageType(nextStorageType);
      setSavedStorageType(nextStorageType);
      setS3Config(nextS3Config);
      setLoadFailed(false);
      storageTypeSave.setStatus("idle");
      s3Save.setStatus("idle");
    },
    [s3Save.setStatus, storageTypeSave.setStatus],
  );

  const beginLoad = useCallback(() => {
    storageTypeSave.setStatus("loading");
    s3Save.setStatus("loading");
  }, [s3Save.setStatus, storageTypeSave.setStatus]);

  const failLoad = useCallback(() => {
    setLoadFailed(true);
    storageTypeSave.setStatus("error");
    s3Save.setStatus("error");
  }, [s3Save.setStatus, storageTypeSave.setStatus]);

  const handleStorageTypeChange = useCallback(
    async (next: StorageType) => {
      if (
        next === storageType ||
        storageTypeSave.status === "loading" ||
        storageTypeSave.status === "saving" ||
        s3Save.status === "saving"
      ) {
        return;
      }

      setStorageType(next);

      if (next === "S3") {
        storageTypeSave.setStatus("idle");
        return;
      }

      const previous = savedStorageType;
      storageTypeSave.setStatus("saving");

      try {
        await settingsApi.setStorageType(next);
        setSavedStorageType(next);
        storageTypeSave.markSaved();
      } catch (error) {
        setStorageType(previous);
        storageTypeSave.setStatus("error");
        showApiErrorToast(
          error,
          t("storageSettings.errors.storageSaveFailed"),
          "admin-storage-settings:storage-type:save-failed",
        );
      }
    },
    [
      s3Save.status,
      savedStorageType,
      storageType,
      storageTypeSave,
      t,
    ],
  );

  const saveS3AndActivate = useCallback(async () => {
    if (
      storageTypeSave.status === "loading" ||
      storageTypeSave.status === "saving" ||
      s3Save.status === "loading" ||
      s3Save.status === "saving"
    ) {
      return;
    }

    storageTypeSave.setStatus("saving");
    s3Save.setStatus("saving");

    try {
      await settingsApi.setS3Config(s3Config);
      await settingsApi.setStorageType("S3");
      setStorageType("S3");
      setSavedStorageType("S3");
      storageTypeSave.markSaved();
      s3Save.markSaved();
    } catch (error) {
      storageTypeSave.setStatus("error");
      s3Save.setStatus("error");
      showApiErrorToast(
        error,
        t("storageSettings.errors.storageSaveFailed"),
        "admin-storage-settings:s3:save-failed",
      );
    }
  }, [s3Config, s3Save, storageTypeSave, t]);

  const storageTypeBusy =
    storageTypeSave.status === "loading" ||
    storageTypeSave.status === "saving";
  const storageTypeDisabled =
    loadFailed ||
    storageTypeBusy ||
    s3Save.status === "saving";
  const s3Disabled =
    loadFailed ||
    s3Save.status === "loading" ||
    s3Save.status === "saving";

  return {
    beginLoad,
    failLoad,
    handleStorageTypeChange,
    initialize,
    s3Config,
    s3Disabled,
    s3Saving:
      s3Save.status === "saving" || storageTypeSave.status === "saving",
    s3Status: s3Save.status,
    saveS3AndActivate,
    setS3Config,
    storageType,
    storageTypeDisabled,
    storageTypeStatus: storageTypeSave.status,
  };
};
