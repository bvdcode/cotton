import { useState } from "react";
import { useTranslation } from "react-i18next";
import { getApiErrorMessage } from "@shared/api/httpClient";
import {
  useEnableVectorExtensionMutation,
  useTriggerFileIndexingMutation,
  useVectorExtensionStatusQuery,
} from "@shared/api/queries/admin";
import { settingsApi } from "@shared/api/settingsApi";
import { toast } from "@shared/ui/notifications";
import { useAutoSavedSetting } from "../settings/useAutoSavedSetting";
import { getVectorSetupFailure } from "./smartSearchSetup";

export const useSmartSearchSetup = () => {
  const { t, i18n } = useTranslation("admin");
  const indexing = useAutoSavedSetting<boolean>({
    initial: false,
    load: settingsApi.getAllowGlobalIndexing,
    save: settingsApi.setAllowGlobalIndexing,
    toastIdPrefix: "admin-smart-search:indexing",
    loadErrorMessage: t("settings.errors.loadFailed"),
    saveErrorMessage: t("settings.errors.saveFailed"),
  });
  const statusQuery = useVectorExtensionStatusQuery();
  const enableMutation = useEnableVectorExtensionMutation();
  const triggerMutation = useTriggerFileIndexingMutation();
  const [environment, setEnvironment] = useState<"docker" | "native">("docker");
  const status = statusQuery.data;
  const progress =
    status && status.fileCount > 0
      ? status.embeddedFileCount / status.fileCount
      : 0;
  const numberFormat = new Intl.NumberFormat(i18n.language);
  const failure = getVectorSetupFailure(enableMutation.error);
  const needsInstallation =
    status &&
    (!status.extensionAvailable || failure === "pgvector_package_missing");
  const busy = statusQuery.isFetching || enableMutation.isPending;
  const activationError =
    failure === null && enableMutation.isError
      ? (getApiErrorMessage(enableMutation.error) ??
        t("smartSearch.errors.enableFailed"))
      : null;
  const triggerIndexing = () => {
    triggerMutation.mutate(undefined, {
      onSuccess: () => toast.success(t("smartSearch.indexingRequested")),
      onError: (error) =>
        toast.error(
          getApiErrorMessage(error) ?? t("smartSearch.errors.triggerFailed"),
        ),
    });
  };
  const refresh = async () => {
    const result = await statusQuery.refetch();
    if (
      result.isSuccess &&
      (result.data.extensionEnabled || failure !== "pgvector_permission_denied")
    ) {
      enableMutation.reset();
    }
  };
  const changeEnvironment = (value: string | null) => {
    if (value === "docker" || value === "native") {
      setEnvironment(value);
    }
  };

  return {
    indexing,
    statusQuery,
    enableMutation,
    triggerMutation,
    environment,
    status,
    progress,
    numberFormat,
    failure,
    needsInstallation,
    busy,
    activationError,
    triggerIndexing,
    refresh,
    changeEnvironment,
    loadError:
      getApiErrorMessage(statusQuery.error) ??
      t("smartSearch.errors.loadFailed"),
    canTriggerIndexing: indexing.savedValue && Boolean(status?.indexReady),
  };
};
