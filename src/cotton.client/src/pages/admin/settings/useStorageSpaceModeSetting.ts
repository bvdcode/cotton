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
  const save = useSaveStatus();

  const initialize = useCallback(
    (next: StorageSpaceMode) => {
      setValue(next);
      setSavedValue(next);
      setLoadFailed(false);
      save.setStatus("idle");
    },
    [save.setStatus],
  );

  const beginLoad = useCallback(() => {
    save.setStatus("loading");
  }, [save.setStatus]);

  const failLoad = useCallback(() => {
    setLoadFailed(true);
    save.setStatus("error");
  }, [save.setStatus]);

  const handleChange = useCallback(
    async (next: StorageSpaceMode | null) => {
      if (
        !next ||
        next === value ||
        save.status === "loading" ||
        save.status === "saving"
      ) {
        return;
      }

      const previous = savedValue;
      setValue(next);
      save.setStatus("saving");

      try {
        await settingsApi.setStorageSpaceMode(next);
        setSavedValue(next);
        save.markSaved();
      } catch (error) {
        setValue(previous);
        save.setStatus("error");
        showApiErrorToast(
          error,
          t("settings.errors.saveFailed"),
          "admin-storage-settings:storage-space-mode:save-failed",
        );
      }
    },
    [save, savedValue, t, value],
  );

  return {
    beginLoad,
    disabled:
      loadFailed ||
      save.status === "loading" ||
      save.status === "saving",
    failLoad,
    handleChange,
    initialize,
    status: save.status,
    value,
  };
};
