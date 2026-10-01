import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { settingsApi, type ComputionMode } from "@shared/api/settingsApi";
import {
  getComputationError,
  type ComputationError,
  type ComputationStatus,
} from "@shared/api/computation";
import { queryKeys } from "@shared/api/queries/queryKeys";
import {
  computionOptions,
  validateRemoteComputationRunnerUrl,
} from "./adminGeneralSettingsModel";
import type { SaveStatus } from "./useAutoSavedSetting";

type ComputationSettings = { mode: ComputionMode; url: string };

const queryKey = ["admin", "computation-settings"] as const;
const serviceQueryKey = queryKeys.admin.computationStatus();

const loadSettings = async (): Promise<ComputationSettings> => {
  const [mode, url] = await Promise.all([
    settingsApi.getComputionMode(),
    settingsApi.getRemoteComputationRunnerUrl(),
  ]);
  return { mode, url: url.trim() };
};

const saveSettings = async (
  value: ComputationSettings,
): Promise<ComputationSettings & { service: ComputationStatus | null }> => {
  switch (value.mode) {
    case "Remote":
      return {
        ...value,
        service: await settingsApi.setRemoteComputationRunnerUrl(value.url),
      };
    case "Local":
    case "Cloud":
      return {
        ...value,
        service: await settingsApi.setComputionMode(value.mode),
      };
  }
};

export const getComputationErrorKey = (
  mode: ComputionMode | undefined,
  failure: ComputationError | null,
) => {
  if (failure === null) {
    return "settings.errors.saveFailed";
  }
  if (mode === "Cloud") {
    return "settings.general.remoteRunner.cloudUnavailable";
  }
  return `settings.general.remoteRunner.errors.${failure}`;
};

export const getConnectedComputationKey = (mode: ComputionMode) => {
  switch (mode) {
    case "Cloud":
      return "settings.general.remoteRunner.cloudConnected";
    case "Local":
    case "Remote":
      return "settings.general.remoteRunner.connected";
  }
};

const isShowingSavedService = (
  mode: ComputionMode,
  normalizedUrl: string | null,
  settings: ComputationSettings | undefined,
) => {
  if (mode !== settings?.mode) {
    return false;
  }
  switch (mode) {
    case "Cloud":
      return true;
    case "Remote":
      return normalizedUrl === settings?.url;
    case "Local":
      return false;
  }
};

export const useComputationModeSetting = () => {
  const { t } = useTranslation("admin");
  const queryClient = useQueryClient();
  const settings = useQuery({
    queryKey,
    queryFn: loadSettings,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnMount: "always",
  });
  const service = useQuery({
    queryKey: [...serviceQueryKey, settings.data?.mode, settings.data?.url],
    queryFn: () => settingsApi.getComputationStatus(),
    enabled: settings.data?.mode !== "Local" && !settings.isFetching,
    retry: false,
    refetchOnWindowFocus: false,
  });
  const [draftMode, setDraftMode] = useState<ComputionMode | null>(null);
  const [draftUrl, setDraftUrl] = useState<string | null>(null);
  const savedSettings: ComputationSettings = settings.data ?? {
    mode: "Local",
    url: "",
  };
  const mode = draftMode ?? savedSettings.mode;
  const url = draftUrl ?? savedSettings.url;
  const save = useMutation({
    mutationFn: saveSettings,
    onError: (_error, value) => {
      if (value.mode !== "Remote") {
        setDraftMode(null);
        setDraftUrl(null);
      }
    },
    onSuccess: async ({ service, ...value }) => {
      await queryClient.cancelQueries({ queryKey: serviceQueryKey });
      if (service !== null) {
        queryClient.setQueryData(
          [...serviceQueryKey, value.mode, value.url],
          service,
        );
      }
      queryClient.setQueryData(queryKey, value);
      setDraftMode(null);
      setDraftUrl(null);
    },
  });
  const validation = validateRemoteComputationRunnerUrl(
    url,
    mode === "Remote",
    t("settings.general.validation.required"),
    t("settings.general.validation.remoteComputationRunnerUrlInvalid"),
  );
  const busy = settings.isFetching || settings.isPending || save.isPending;
  const disabled = busy || settings.isError;
  const showingSavedService = isShowingSavedService(
    mode,
    validation.normalized,
    settings.data,
  );
  const savedService =
    showingSavedService && !service.isError ? service.data : null;
  const status: SaveStatus = settings.isPending
    ? "loading"
    : save.isPending
      ? "saving"
      : "idle";

  const selectMode = (next: string) => {
    const selected = computionOptions.find((option) => option === next);
    if (selected === undefined) {
      return;
    }
    save.reset();
    setDraftMode(selected);
    if (selected !== "Remote") {
      save.mutate({ mode: selected, url: settings.data?.url ?? "" });
    }
  };
  const changeUrl = (value: string) => {
    setDraftUrl(value);
    save.reset();
  };
  const submitRemote = () => {
    if (!disabled && mode === "Remote" && validation.normalized !== null) {
      save.mutate({ mode, url: validation.normalized });
    }
  };

  return {
    mode,
    url,
    settings,
    save,
    validation,
    disabled,
    status,
    savedService,
    showingSavedService,
    service,
    selectMode,
    changeUrl,
    submitRemote,
    saveError: t(
      getComputationErrorKey(
        save.variables?.mode,
        getComputationError(save.error),
      ),
    ),
  };
};
