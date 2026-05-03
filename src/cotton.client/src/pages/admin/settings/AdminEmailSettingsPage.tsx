import {
  Alert,
  Box,
  FormControl,
  FormControlLabel,
  InputLabel,
  LinearProgress,
  MenuItem,
  Paper,
  Select,
  Stack,
  Switch,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "react-toastify";
import {
  settingsApi,
  type EmailConfig,
  type EmailMode,
} from "../../../shared/api/settingsApi";
import { AdminSettingSaveIconButton } from "./AdminSettingSaveIconButton";
import { AdminSettingSavingOverlay } from "./AdminSettingSavingOverlay";

type SavingKey = "emailMode" | "smtpConfig";

const emailModes: EmailMode[] = ["None", "Cloud", "Custom"];
const saveIconSx = { mt: 1, flexShrink: 0 };

export const AdminEmailSettingsPage = () => {
  const { t } = useTranslation("admin");
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [savingKey, setSavingKey] = useState<SavingKey | null>(null);
  const [emailMode, setEmailMode] = useState<EmailMode>("None");
  const [emailConfig, setEmailConfig] = useState<EmailConfig>({
    smtpServer: "",
    port: "",
    username: "",
    password: "",
    fromAddress: "",
    useSSL: false,
  });

  const isBusy = loading || savingKey !== null;
  const isCustomEmailMode = emailMode === "Custom";
  const isSaving = (key: SavingKey): boolean => savingKey === key;

  useEffect(() => {
    let active = true;

    const load = async () => {
      setLoading(true);
      setLoadError(null);

      try {
        const [nextEmailMode, nextEmailConfig] = await Promise.all([
          settingsApi.getEmailMode(),
          settingsApi.getEmailConfig(),
        ]);

        if (!active) return;

        setEmailMode(nextEmailMode);
        setEmailConfig(nextEmailConfig);
      } catch {
        if (!active) return;
        setLoadError(t("emailSettings.errors.loadFailed"));
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    };

    void load();

    return () => {
      active = false;
    };
  }, [t]);

  const updateEmailConfig = (
    key: keyof EmailConfig,
    value: string | boolean,
  ) => {
    setEmailConfig((current) => ({ ...current, [key]: value }));
  };

  const saveSmtpConfig = async () => {
    if (isBusy) return;

    setSavingKey("smtpConfig");
    try {
      await settingsApi.setEmailConfig(emailConfig);
      toast.success(t("emailSettings.state.smtpSaved"), {
        toastId: "admin-email-settings:smtp-config:saved",
      });
    } catch {
      toast.error(t("emailSettings.errors.smtpSaveFailed"), {
        toastId: "admin-email-settings:smtp-config:save-failed",
      });
    } finally {
      setSavingKey(null);
    }
  };

  const saveEmailMode = async () => {
    if (isBusy) return;

    setSavingKey("emailMode");
    try {
      if (emailMode === "Custom") {
        await settingsApi.setEmailConfig(emailConfig);
      }

      await settingsApi.setEmailMode(emailMode);
      toast.success(t("emailSettings.state.modeSaved"), {
        toastId: "admin-email-settings:email-mode:saved",
      });
    } catch {
      toast.error(t("emailSettings.errors.modeSaveFailed"), {
        toastId: "admin-email-settings:email-mode:save-failed",
      });
    } finally {
      setSavingKey(null);
    }
  };

  return (
    <Stack spacing={2}>
      <Paper>
        <Stack p={2} spacing={2}>
          <Stack spacing={0.5}>
            <Typography variant="h6" fontWeight={700}>
              {t("emailSettings.title")}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {t("emailSettings.description")}
            </Typography>
          </Stack>

          <LinearProgress
            sx={{
              opacity: isBusy ? 1 : 0,
              transition: "opacity 120ms ease",
            }}
          />

          {loadError && <Alert severity="error">{loadError}</Alert>}

          <Stack direction="row" spacing={1} alignItems="flex-start">
            <Box flex={1} minWidth={0}>
              <AdminSettingSavingOverlay saving={loading}>
                <FormControl fullWidth>
                  <InputLabel id="admin-email-mode-label">
                    {t("emailSettings.fields.emailMode")}
                  </InputLabel>
                  <Select
                    labelId="admin-email-mode-label"
                    label={t("emailSettings.fields.emailMode")}
                    value={emailMode}
                    onChange={(event) =>
                      setEmailMode(event.target.value as EmailMode)
                    }
                    disabled={isBusy}
                  >
                    {emailModes.map((mode) => (
                      <MenuItem key={mode} value={mode}>
                        {t(`emailSettings.emailMode.${mode}`)}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              </AdminSettingSavingOverlay>
            </Box>
            <AdminSettingSavingOverlay saving={isSaving("emailMode")}>
              <AdminSettingSaveIconButton
                label={t("emailSettings.actions.saveMode")}
                onClick={() => void saveEmailMode()}
                disabled={isBusy}
                sx={saveIconSx}
              />
            </AdminSettingSavingOverlay>
          </Stack>

          {isCustomEmailMode && (
            <Stack spacing={2}>
              <Typography variant="subtitle1" fontWeight={700}>
                {t("emailSettings.smtp.title")}
              </Typography>
              <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
                <Box flex={1} minWidth={0}>
                  <AdminSettingSavingOverlay saving={loading}>
                    <TextField
                      label={t("emailSettings.smtp.fields.smtpServer")}
                      value={emailConfig.smtpServer}
                      onChange={(event) =>
                        updateEmailConfig("smtpServer", event.target.value)
                      }
                      disabled={isBusy}
                      fullWidth
                    />
                  </AdminSettingSavingOverlay>
                </Box>
                <Box flex={1} minWidth={0}>
                  <AdminSettingSavingOverlay saving={loading}>
                    <TextField
                      label={t("emailSettings.smtp.fields.port")}
                      value={emailConfig.port}
                      onChange={(event) =>
                        updateEmailConfig("port", event.target.value)
                      }
                      disabled={isBusy}
                      fullWidth
                    />
                  </AdminSettingSavingOverlay>
                </Box>
              </Stack>
              <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
                <Box flex={1} minWidth={0}>
                  <AdminSettingSavingOverlay saving={loading}>
                    <TextField
                      label={t("emailSettings.smtp.fields.username")}
                      value={emailConfig.username}
                      onChange={(event) =>
                        updateEmailConfig("username", event.target.value)
                      }
                      disabled={isBusy}
                      fullWidth
                    />
                  </AdminSettingSavingOverlay>
                </Box>
                <Box flex={1} minWidth={0}>
                  <AdminSettingSavingOverlay saving={loading}>
                    <TextField
                      label={t("emailSettings.smtp.fields.password")}
                      value={emailConfig.password}
                      onChange={(event) =>
                        updateEmailConfig("password", event.target.value)
                      }
                      disabled={isBusy}
                      type="password"
                      fullWidth
                    />
                  </AdminSettingSavingOverlay>
                </Box>
              </Stack>
              <Stack direction="row" spacing={1} alignItems="flex-start">
                <Box flex={1} minWidth={0}>
                  <AdminSettingSavingOverlay saving={loading}>
                    <TextField
                      label={t("emailSettings.smtp.fields.fromAddress")}
                      value={emailConfig.fromAddress}
                      onChange={(event) =>
                        updateEmailConfig("fromAddress", event.target.value)
                      }
                      disabled={isBusy}
                      fullWidth
                    />
                  </AdminSettingSavingOverlay>
                </Box>
                <AdminSettingSavingOverlay saving={isSaving("smtpConfig")}>
                  <AdminSettingSaveIconButton
                    label={t("emailSettings.actions.saveSmtp")}
                    onClick={() => void saveSmtpConfig()}
                    disabled={isBusy}
                    sx={saveIconSx}
                  />
                </AdminSettingSavingOverlay>
              </Stack>
              <AdminSettingSavingOverlay saving={loading}>
                <FormControlLabel
                  control={
                    <Switch
                      checked={emailConfig.useSSL}
                      onChange={(event) =>
                        updateEmailConfig("useSSL", event.target.checked)
                      }
                      disabled={isBusy}
                    />
                  }
                  label={t("emailSettings.smtp.fields.useSSL")}
                />
              </AdminSettingSavingOverlay>
            </Stack>
          )}
        </Stack>
      </Paper>
    </Stack>
  );
};
