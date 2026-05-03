import {
  Alert,
  Box,
  Divider,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import {
  useEffect,
  useMemo,
  useState,
  type Dispatch,
  type SetStateAction,
} from "react";
import { useTranslation } from "react-i18next";
import { toast } from "react-toastify";
import {
  settingsApi,
  type EmailConfig,
  type EmailMode,
} from "../../../shared/api/settingsApi";
import {
  hasApiErrorToastBeenDispatched,
  isAxiosError,
} from "../../../shared/api/httpClient";
import { EmailModeSelector } from "./EmailModeSelector";
import { SmtpConfigForm } from "./SmtpConfigForm";
import type { SaveStatus } from "./useAutoSavedSetting";

const SAVED_FLASH_MS = 1500;
type FlashTimers = {
  mode: number | null;
  smtp: number | null;
};

export const AdminNotificationsSettingsPage = () => {
  const { t } = useTranslation("admin");
  const [modeStatus, setModeStatus] = useState<SaveStatus>("loading");
  const [smtpStatus, setSmtpStatus] = useState<SaveStatus>("loading");
  const [loadError, setLoadError] = useState<string | null>(null);
  const [emailMode, setEmailMode] = useState<EmailMode>("None");
  const [savedEmailMode, setSavedEmailMode] = useState<EmailMode>("None");
  const [emailConfig, setEmailConfig] = useState<EmailConfig>({
    smtpServer: "",
    port: "",
    username: "",
    password: "",
    fromAddress: "",
    useSSL: false,
  });

  const flashTimers = useMemo<FlashTimers>(
    () => ({
      mode: null,
      smtp: null,
    }),
    [],
  );

  const isBusy = modeStatus === "loading" || modeStatus === "saving";
  const isSmtpBusy = smtpStatus === "loading" || smtpStatus === "saving";
  const isCustomEmailMode = emailMode === "Custom";

  const flashStatus = (
    setStatus: Dispatch<SetStateAction<SaveStatus>>,
    key: keyof FlashTimers,
  ) => {
    const pendingTimer = flashTimers[key];
    if (pendingTimer !== null) {
      window.clearTimeout(pendingTimer);
    }
    setStatus("saved");
    flashTimers[key] = window.setTimeout(() => {
      setStatus((current) => (current === "saved" ? "idle" : current));
      flashTimers[key] = null;
    }, SAVED_FLASH_MS);
  };

  useEffect(() => {
    let active = true;

    const load = async () => {
      setModeStatus("loading");
      setSmtpStatus("loading");
      setLoadError(null);

      try {
        const [nextEmailMode, nextEmailConfig] = await Promise.all([
          settingsApi.getEmailMode(),
          settingsApi.getEmailConfig(),
        ]);

        if (!active) return;

        setEmailMode(nextEmailMode);
        setSavedEmailMode(nextEmailMode);
        setEmailConfig(nextEmailConfig);
        setModeStatus("idle");
        setSmtpStatus("idle");
      } catch {
        if (!active) return;
        setLoadError(t("notificationsSettings.errors.loadFailed"));
        setModeStatus("idle");
        setSmtpStatus("idle");
      } finally {
        // handled by explicit statuses
      }
    };

    void load();

    return () => {
      active = false;
      if (flashTimers.mode !== null) {
        window.clearTimeout(flashTimers.mode);
        flashTimers.mode = null;
      }
      if (flashTimers.smtp !== null) {
        window.clearTimeout(flashTimers.smtp);
        flashTimers.smtp = null;
      }
    };
  }, [flashTimers, t]);

  const handleEmailModeChange = async (next: EmailMode) => {
    if (next === emailMode || isBusy || isSmtpBusy) return;

    setEmailMode(next);

    if (next === "Custom") {
      setModeStatus("idle");
      return;
    }

    const previous = savedEmailMode;
    setModeStatus("saving");
    try {
      await settingsApi.setEmailMode(next);
      setSavedEmailMode(next);
      flashStatus(setModeStatus, "mode");
    } catch (error) {
      setEmailMode(previous);
      setModeStatus("error");
      if (!isAxiosError(error) || !hasApiErrorToastBeenDispatched(error)) {
        toast.error(t("notificationsSettings.errors.modeSaveFailed"), {
          toastId: "admin-notifications-settings:email-mode:save-failed",
        });
      }
    }
  };

  const saveSmtpSettings = async () => {
    if (isBusy || isSmtpBusy) return;

    setModeStatus("saving");
    setSmtpStatus("saving");
    try {
      await settingsApi.setEmailConfig(emailConfig);
      await settingsApi.setEmailMode("Custom");
      setSavedEmailMode("Custom");
      setEmailMode("Custom");
      flashStatus(setModeStatus, "mode");
      flashStatus(setSmtpStatus, "smtp");
    } catch (error) {
      setModeStatus("error");
      setSmtpStatus("error");
      if (!isAxiosError(error) || !hasApiErrorToastBeenDispatched(error)) {
        toast.error(t("notificationsSettings.errors.modeSaveFailed"), {
          toastId: "admin-notifications-settings:email-mode:save-failed",
        });
      }
    }
  };

  return (
    <Stack>
      <Box sx={{ width: "100%", display: "flex", justifyContent: "center" }}>
      <Paper
        sx={{
          width: "min(100%, 880px)",
          overflow: "hidden",
        }}
      >
        <Stack p={3} spacing={3} divider={<Divider flexItem />}>
          <Typography variant="h5" fontWeight={700}>
            {t("notificationsSettings.title")}
          </Typography>

          {loadError && <Alert severity="error">{loadError}</Alert>}

          <EmailModeSelector
            value={emailMode}
            onChange={(next) => void handleEmailModeChange(next)}
            disabled={isBusy || isSmtpBusy}
            status={modeStatus}
          />

          {isCustomEmailMode && (
            <SmtpConfigForm
              config={emailConfig}
              onChange={setEmailConfig}
              onSave={() => void saveSmtpSettings()}
              saving={smtpStatus === "saving"}
              disabled={isBusy || isSmtpBusy}
              status={smtpStatus}
            />
          )}
        </Stack>
      </Paper>
      </Box>
    </Stack>
  );
};
