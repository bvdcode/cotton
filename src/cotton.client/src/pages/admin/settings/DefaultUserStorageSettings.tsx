import { Stack, TextField } from "@mui/material";
import {
  useEffect,
  useRef,
  useState,
  type Dispatch,
  type MutableRefObject,
  type SetStateAction,
} from "react";
import { useTranslation } from "react-i18next";
import { showApiErrorToast } from "../../../shared/api/httpClient";
import { settingsApi } from "../../../shared/api/settingsApi";
import { isGuidString } from "../../../shared/utils/guid";
import { SAVED_STATUS_VISIBLE_MS } from "./adminSettingSaveStatus";
import { SettingsSaveButton } from "./SettingsSaveButton";
import { SettingsSection } from "./SettingsSection";
import type { SaveStatus } from "./useAutoSavedSetting";

interface DefaultUserStorageSettingsProps {
  defaultUserQuotaBytes: number | null | undefined;
  defaultTemplateNodeId: string | null | undefined;
  loadFailed: boolean;
}

const bytesPerGiB = 1024 ** 3;

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

const flashSavedStatus = (
  setStatus: Dispatch<SetStateAction<SaveStatus>>,
  timer: MutableRefObject<number | null>,
): void => {
  if (timer.current !== null) {
    window.clearTimeout(timer.current);
  }
  setStatus("saved");
  timer.current = window.setTimeout(() => {
    setStatus((current) => (current === "saved" ? "idle" : current));
    timer.current = null;
  }, SAVED_STATUS_VISIBLE_MS);
};

const resolveStatus = (
  loadFailed: boolean,
  loadedValue: number | string | null | undefined,
  localStatus: SaveStatus,
): SaveStatus => {
  if (loadFailed) {
    return "error";
  }

  if (loadedValue === undefined) {
    return "loading";
  }

  return localStatus;
};

export const DefaultUserStorageSettings = ({
  defaultUserQuotaBytes,
  defaultTemplateNodeId,
  loadFailed,
}: DefaultUserStorageSettingsProps) => {
  const { t } = useTranslation("admin");
  const loadedQuotaInput =
    defaultUserQuotaBytes === undefined
      ? ""
      : formatQuotaInput(defaultUserQuotaBytes);
  const loadedTemplateInput = defaultTemplateNodeId ?? "";
  const [quotaInputOverride, setQuotaInputOverride] = useState<string | null>(
    null,
  );
  const [savedQuotaInputOverride, setSavedQuotaInputOverride] = useState<
    string | null
  >(null);
  const [quotaInvalid, setQuotaInvalid] = useState(false);
  const [localQuotaStatus, setLocalQuotaStatus] =
    useState<SaveStatus>("idle");
  const [templateInputOverride, setTemplateInputOverride] = useState<
    string | null
  >(null);
  const [savedTemplateInputOverride, setSavedTemplateInputOverride] = useState<
    string | null
  >(null);
  const [templateInvalid, setTemplateInvalid] = useState(false);
  const [localTemplateStatus, setLocalTemplateStatus] =
    useState<SaveStatus>("idle");
  const quotaInput = quotaInputOverride ?? loadedQuotaInput;
  const savedQuotaInput = savedQuotaInputOverride ?? loadedQuotaInput;
  const templateInput = templateInputOverride ?? loadedTemplateInput;
  const savedTemplateInput =
    savedTemplateInputOverride ?? loadedTemplateInput;
  const quotaStatus = resolveStatus(
    loadFailed,
    defaultUserQuotaBytes,
    localQuotaStatus,
  );
  const templateStatus = resolveStatus(
    loadFailed,
    defaultTemplateNodeId,
    localTemplateStatus,
  );
  const quotaTimer = useRef<number | null>(null);
  const templateTimer = useRef<number | null>(null);

  useEffect(
    () => () => {
      if (quotaTimer.current !== null) {
        window.clearTimeout(quotaTimer.current);
      }
      if (templateTimer.current !== null) {
        window.clearTimeout(templateTimer.current);
      }
    },
    [],
  );

  const saveQuota = async (): Promise<void> => {
    if (quotaStatus === "loading" || quotaStatus === "saving") {
      return;
    }

    setQuotaInvalid(false);
    let quotaBytes: number | null;
    try {
      quotaBytes = parseQuotaInput(quotaInput);
    } catch {
      setQuotaInvalid(true);
      setLocalQuotaStatus("error");
      return;
    }

    const previous = savedQuotaInput;
    setLocalQuotaStatus("saving");
    try {
      await settingsApi.setDefaultUserStorageQuotaBytes(quotaBytes);
      const saved = formatQuotaInput(quotaBytes);
      setQuotaInputOverride(saved);
      setSavedQuotaInputOverride(saved);
      flashSavedStatus(setLocalQuotaStatus, quotaTimer);
    } catch (error) {
      setQuotaInputOverride(previous);
      setLocalQuotaStatus("error");
      showApiErrorToast(
        error,
        t("storageSettings.errors.quotaSaveFailed"),
        "admin-storage-settings:quota:save-failed",
      );
    }
  };

  const saveTemplateNode = async (): Promise<void> => {
    if (templateStatus === "loading" || templateStatus === "saving") {
      return;
    }

    setTemplateInvalid(false);
    let nodeId: string | null;
    try {
      nodeId = parseTemplateNodeIdInput(templateInput);
    } catch {
      setTemplateInvalid(true);
      setLocalTemplateStatus("error");
      return;
    }

    const previous = savedTemplateInput;
    setLocalTemplateStatus("saving");
    try {
      await settingsApi.setDefaultUserTemplateNodeId(nodeId);
      const saved = nodeId ?? "";
      setTemplateInputOverride(saved);
      setSavedTemplateInputOverride(saved);
      flashSavedStatus(setLocalTemplateStatus, templateTimer);
    } catch (error) {
      setTemplateInputOverride(previous);
      setLocalTemplateStatus("error");
      showApiErrorToast(
        error,
        t("storageSettings.errors.templateSaveFailed"),
        "admin-storage-settings:template:save-failed",
      );
    }
  };

  const quotaDisabled =
    loadFailed || quotaStatus === "loading" || quotaStatus === "saving";
  const templateDisabled =
    loadFailed || templateStatus === "loading" || templateStatus === "saving";

  return (
    <>
      <SettingsSection
        title={t("storageSettings.quota.title")}
        description={t("storageSettings.quota.description")}
        status={quotaStatus}
      >
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          alignItems={{ xs: "stretch", sm: "flex-start" }}
        >
          <TextField
            label={t("storageSettings.quota.fields.defaultUserQuotaGiB")}
            value={quotaInput}
            onChange={(event) => {
              setQuotaInputOverride(event.target.value);
              setQuotaInvalid(false);
              if (quotaStatus === "error") {
                setLocalQuotaStatus("idle");
              }
            }}
            disabled={quotaDisabled}
            error={quotaInvalid || quotaStatus === "error"}
            helperText={
              quotaInvalid
                ? t("storageSettings.errors.quotaInvalid")
                : t("storageSettings.quota.help")
            }
            type="number"
            inputProps={{ min: 0, step: 0.25 }}
            fullWidth
          />
          <SettingsSaveButton
            changed={quotaInput !== savedQuotaInput}
            disabled={quotaDisabled}
            label={t("settings.actions.save")}
            onSave={() => void saveQuota()}
            saving={quotaStatus === "saving"}
          />
        </Stack>
      </SettingsSection>

      <SettingsSection
        title={t("storageSettings.template.title")}
        description={t("storageSettings.template.description")}
        status={templateStatus}
      >
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          alignItems={{ xs: "stretch", sm: "flex-start" }}
        >
          <TextField
            label={t("storageSettings.template.fields.nodeId")}
            value={templateInput}
            onChange={(event) => {
              setTemplateInputOverride(event.target.value);
              setTemplateInvalid(false);
              if (templateStatus === "error") {
                setLocalTemplateStatus("idle");
              }
            }}
            disabled={templateDisabled}
            error={templateInvalid || templateStatus === "error"}
            helperText={
              templateInvalid
                ? t("storageSettings.errors.templateNodeIdInvalid")
                : t("storageSettings.template.help")
            }
            fullWidth
          />
          <SettingsSaveButton
            changed={templateInput !== savedTemplateInput}
            disabled={templateDisabled}
            label={t("settings.actions.save")}
            onSave={() => void saveTemplateNode()}
            saving={templateStatus === "saving"}
          />
        </Stack>
      </SettingsSection>
    </>
  );
};
