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
  const {
    markSaved: markStorageTypeSaved,
    setStatus: setStorageTypeStatus,
    status: storageTypeStatus,
  } = useSaveStatus();
  const {
    markSaved: markS3Saved,
    setStatus: setS3Status,
    status: s3Status,
  } = useSaveStatus();

  const initialize = useCallback(
    (nextStorageType: StorageType, nextS3Config: S3Config) => {
      setStorageType(nextStorageType);
      setSavedStorageType(nextStorageType);
      setS3Config(nextS3Config);
      setLoadFailed(false);
      setStorageTypeStatus("idle");
      setS3Status("idle");
    },
    [setS3Status, setStorageTypeStatus],
  );

  const beginLoad = useCallback(() => {
    setStorageTypeStatus("loading");
    setS3Status("loading");
  }, [setS3Status, setStorageTypeStatus]);

  const failLoad = useCallback(() => {
    setLoadFailed(true);
    setStorageTypeStatus("error");
    setS3Status("error");
  }, [setS3Status, setStorageTypeStatus]);

  const handleStorageTypeChange = useCallback(
    async (next: StorageType) => {
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
        markStorageTypeSaved();
      } catch (error) {
        setStorageType(previous);
        setStorageTypeStatus("error");
        showApiErrorToast(
          error,
          t("storageSettings.errors.storageSaveFailed"),
          "admin-storage-settings:storage-type:save-failed",
        );
      }
    },
    [
      markStorageTypeSaved,
      s3Status,
      savedStorageType,
      setStorageTypeStatus,
      storageType,
      storageTypeStatus,
      t,
    ],
  );

  const saveS3AndActivate = useCallback(async () => {
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
      markStorageTypeSaved();
      markS3Saved();
    } catch (error) {
      setStorageTypeStatus("error");
      setS3Status("error");
      showApiErrorToast(
        error,
        t("storageSettings.errors.storageSaveFailed"),
        "admin-storage-settings:s3:save-failed",
      );
    }
  }, [
    markS3Saved,
    markStorageTypeSaved,
    s3Config,
    s3Status,
    setS3Status,
    setStorageTypeStatus,
    storageTypeStatus,
    t,
  ]);

  const storageTypeBusy =
    storageTypeStatus === "loading" || storageTypeStatus === "saving";
  const storageTypeDisabled =
    loadFailed ||
    storageTypeBusy ||
    s3Status === "saving";
  const s3Disabled =
    loadFailed ||
    s3Status === "loading" || s3Status === "saving";

  return {
    beginLoad,
    failLoad,
    handleStorageTypeChange,
    initialize,
    s3Config,
    s3Disabled,
    s3Saving:
      s3Status === "saving" || storageTypeStatus === "saving",
    s3Status,
    saveS3AndActivate,
    setS3Config,
    storageType,
    storageTypeDisabled,
    storageTypeStatus,
  };
};
