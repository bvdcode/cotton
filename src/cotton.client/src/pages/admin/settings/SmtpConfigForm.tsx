import SaveIcon from "@mui/icons-material/Save";
import {
  Box,
  Button,
  CircularProgress,
  FormControlLabel,
  Stack,
  Switch,
  TextField,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { EmailConfig } from "../../../shared/api/settingsApi";

type SmtpConfigFormProps = {
  config: EmailConfig;
  onChange: (next: EmailConfig) => void;
  onSave: () => void;
  saving: boolean;
  disabled: boolean;
};

export const SmtpConfigForm = ({
  config,
  onChange,
  onSave,
  saving,
  disabled,
}: SmtpConfigFormProps) => {
  const { t } = useTranslation("admin");

  const update = <K extends keyof EmailConfig>(key: K, value: EmailConfig[K]) =>
    onChange({ ...config, [key]: value });

  return (
    <Stack spacing={2}>
      <Typography variant="subtitle1" fontWeight={700}>
        {t("emailSettings.smtp.title")}
      </Typography>

      <Box
        sx={{
          display: "grid",
          gap: 2,
          gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" },
        }}
      >
        <TextField
          label={t("emailSettings.smtp.fields.smtpServer")}
          value={config.smtpServer}
          onChange={(event) => update("smtpServer", event.target.value)}
          disabled={disabled}
          fullWidth
        />
        <TextField
          label={t("emailSettings.smtp.fields.port")}
          value={config.port}
          onChange={(event) => update("port", event.target.value)}
          disabled={disabled}
          fullWidth
        />
        <TextField
          label={t("emailSettings.smtp.fields.username")}
          value={config.username}
          onChange={(event) => update("username", event.target.value)}
          disabled={disabled}
          fullWidth
        />
        <TextField
          label={t("emailSettings.smtp.fields.password")}
          type="password"
          value={config.password}
          onChange={(event) => update("password", event.target.value)}
          disabled={disabled}
          fullWidth
        />
      </Box>

      <TextField
        label={t("emailSettings.smtp.fields.fromAddress")}
        value={config.fromAddress}
        onChange={(event) => update("fromAddress", event.target.value)}
        disabled={disabled}
        fullWidth
      />

      <FormControlLabel
        control={
          <Switch
            checked={config.useSSL}
            onChange={(event) => update("useSSL", event.target.checked)}
            disabled={disabled}
          />
        }
        label={t("emailSettings.smtp.fields.useSSL")}
      />

      <Box>
        <Button
          variant="contained"
          onClick={onSave}
          disabled={disabled || saving}
          startIcon={
            saving ? (
              <CircularProgress size={16} color="inherit" />
            ) : (
              <SaveIcon />
            )
          }
        >
          {t("settings.actions.save")}
        </Button>
      </Box>
    </Stack>
  );
};
