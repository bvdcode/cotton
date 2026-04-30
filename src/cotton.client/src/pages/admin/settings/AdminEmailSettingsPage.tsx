import {
  Alert,
  Button,
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
import {
  settingsApi,
  type EmailConfig,
  type EmailMode,
} from "../../../shared/api/settingsApi";

type LoadState =
  | { kind: "loading" }
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "error"; message: string }
  | { kind: "success"; message: string };

const emailModes: EmailMode[] = ["None", "Cloud", "Custom"];

export const AdminEmailSettingsPage = () => {
  const { t } = useTranslation("admin");
  const [loadState, setLoadState] = useState<LoadState>({ kind: "loading" });
  const [emailMode, setEmailMode] = useState<EmailMode>("None");
  const [emailConfig, setEmailConfig] = useState<EmailConfig>({
    smtpServer: "",
    port: "",
    username: "",
    password: "",
    fromAddress: "",
    useSSL: false,
  });

  const isBusy = loadState.kind === "loading" || loadState.kind === "saving";

  useEffect(() => {
    let active = true;

    const load = async () => {
      try {
        const [nextEmailMode, nextEmailConfig] = await Promise.all([
          settingsApi.getEmailMode(),
          settingsApi.getEmailConfig(),
        ]);

        if (!active) return;

        setEmailMode(nextEmailMode);
        setEmailConfig(nextEmailConfig);
        setLoadState({ kind: "idle" });
      } catch {
        if (!active) return;
        setLoadState({
          kind: "error",
          message: t("emailSettings.errors.loadFailed"),
        });
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
    setLoadState({ kind: "saving" });
    try {
      await settingsApi.setEmailConfig(emailConfig);
      setLoadState({
        kind: "success",
        message: t("emailSettings.state.smtpSaved"),
      });
    } catch {
      setLoadState({
        kind: "error",
        message: t("emailSettings.errors.smtpSaveFailed"),
      });
    }
  };

  const saveEmailMode = async () => {
    setLoadState({ kind: "saving" });
    try {
      if (emailMode === "Custom") {
        await settingsApi.setEmailConfig(emailConfig);
      }

      await settingsApi.setEmailMode(emailMode);
      setLoadState({
        kind: "success",
        message: t("emailSettings.state.modeSaved"),
      });
    } catch {
      setLoadState({
        kind: "error",
        message: t("emailSettings.errors.modeSaveFailed"),
      });
    }
  };

  return (
    <Stack spacing={2}>
      <Paper sx={{ overflow: "hidden" }}>
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

          {loadState.kind === "error" && (
            <Alert severity="error">{loadState.message}</Alert>
          )}
          {loadState.kind === "success" && (
            <Alert severity="success">{loadState.message}</Alert>
          )}

          <FormControl fullWidth>
            <InputLabel id="admin-email-mode-label">
              {t("emailSettings.fields.emailMode")}
            </InputLabel>
            <Select
              labelId="admin-email-mode-label"
              label={t("emailSettings.fields.emailMode")}
              value={emailMode}
              onChange={(event) => setEmailMode(event.target.value as EmailMode)}
              disabled={isBusy}
            >
              {emailModes.map((mode) => (
                <MenuItem key={mode} value={mode}>
                  {t(`emailSettings.emailMode.${mode}`)}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <Stack spacing={2}>
            <Typography variant="subtitle1" fontWeight={700}>
              {t("emailSettings.smtp.title")}
            </Typography>
            <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
              <TextField
                label={t("emailSettings.smtp.fields.smtpServer")}
                value={emailConfig.smtpServer}
                onChange={(event) =>
                  updateEmailConfig("smtpServer", event.target.value)
                }
                disabled={isBusy}
                fullWidth
              />
              <TextField
                label={t("emailSettings.smtp.fields.port")}
                value={emailConfig.port}
                onChange={(event) =>
                  updateEmailConfig("port", event.target.value)
                }
                disabled={isBusy}
                fullWidth
              />
            </Stack>
            <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
              <TextField
                label={t("emailSettings.smtp.fields.username")}
                value={emailConfig.username}
                onChange={(event) =>
                  updateEmailConfig("username", event.target.value)
                }
                disabled={isBusy}
                fullWidth
              />
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
            </Stack>
            <TextField
              label={t("emailSettings.smtp.fields.fromAddress")}
              value={emailConfig.fromAddress}
              onChange={(event) =>
                updateEmailConfig("fromAddress", event.target.value)
              }
              disabled={isBusy}
              fullWidth
            />
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
          </Stack>

          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={1}
            useFlexGap
            sx={{ flexWrap: "wrap" }}
          >
            <Button
              variant="outlined"
              onClick={saveSmtpConfig}
              disabled={isBusy}
            >
              {t("emailSettings.actions.saveSmtp")}
            </Button>
            <Button
              variant="contained"
              onClick={saveEmailMode}
              disabled={isBusy}
            >
              {loadState.kind === "saving"
                ? t("emailSettings.actions.saving")
                : t("emailSettings.actions.saveMode")}
            </Button>
          </Stack>
        </Stack>
      </Paper>
    </Stack>
  );
};
