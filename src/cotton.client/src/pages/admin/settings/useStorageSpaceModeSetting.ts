import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  settingsApi,
  type StorageSpaceMode,
} from "@shared/api/settingsApi";
import { showApiErrorToast } from "@shared/api/httpClient";
import { useSaveStatus } from "./useSaveStatus";

export const useStorageSpaceModeSetting = () => {
  const { t } = useTranslation("admin");
  const [value, setValue] = useState<StorageSpaceMode>("Optimal");
  const [savedValue, setSavedValue] = useState<StorageSpaceMode>("Optimal");
  const [loadFailed, setLoadFailed] = useState(false);
  const { markSaved, setStatus, status } = useSaveStatus();

  const initialize = useCallback(
    (next: StorageSpaceMode) => {
      setValue(next);
      setSavedValue(next);
      setLoadFailed(false);
      setStatus("idle");
    },
    [setStatus],
  );

  const beginLoad = useCallback(() => {
    setStatus("loading");
  }, [setStatus]);

  const failLoad = useCallback(() => {
    setLoadFailed(true);
    setStatus("error");
  }, [setStatus]);

  const handleChange = useCallback(
    async (next: StorageSpaceMode | null) => {
      if (
        !next ||
        next === value ||
        status === "loading" ||
        status === "saving"
      ) {
        return;
      }

      const previous = savedValue;
      setValue(next);
      setStatus("saving");

      try {
        await settingsApi.setStorageSpaceMode(next);
        setSavedValue(next);
        markSaved();
      } catch (error) {
        setValue(previous);
        setStatus("error");
        showApiErrorToast(
          error,
          t("settings.errors.saveFailed"),
          "admin-storage-settings:storage-space-mode:save-failed",
        );
      }
    },
    [markSaved, savedValue, setStatus, status, t, value],
  );

  return {
    beginLoad,
    disabled:
      loadFailed ||
      status === "loading" ||
      status === "saving",
    failLoad,
    handleChange,
    initialize,
    status,
    value,
  };
};
