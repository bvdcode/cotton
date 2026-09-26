import {
  Box,
  Button,
  Alert,
  CircularProgress,
  Typography,
  Stack,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  IconButton,
  InputAdornment,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { TotpSetup } from "../../../shared/api/totpApi";
import { TotpSetupForm } from "./TotpSetupForm";
import SecurityIcon from "@mui/icons-material/Security";
import SecurityOutlinedIcon from "@mui/icons-material/SecurityOutlined";
import NoEncryptionOutlinedIcon from "@mui/icons-material/NoEncryptionOutlined";
import VisibilityIcon from "@mui/icons-material/Visibility";
import VisibilityOffIcon from "@mui/icons-material/VisibilityOff";

const formatDateTime = (iso: string): string => {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return iso;
  }
  return new Intl.DateTimeFormat(undefined, {
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
};

type TotpStatusIconProps = {
  enabled: boolean;
};

export const TotpStatusIcon = ({ enabled }: TotpStatusIconProps) =>
  enabled ? (
    <SecurityIcon color="primary" />
  ) : (
    <SecurityOutlinedIcon color="primary" />
  );

type EnabledTotpContentProps = {
  enabledAt: string | null;
  failedAttempts: number;
  onDisable: () => void;
};

export const EnabledTotpContent = ({
  enabledAt,
  failedAttempts,
  onDisable,
}: EnabledTotpContentProps) => {
  const { t } = useTranslation("profile");

  return (
    <Stack spacing={2} paddingY={2}>
      {enabledAt && (
        <Box display="flex" justifyContent="space-between" gap={2}>
          <Typography variant="body2" color="text.secondary">
            {t("fields.totpEnabledAt")}
          </Typography>
          <Typography variant="body2" fontWeight={600} textAlign="right">
            {formatDateTime(enabledAt)}
          </Typography>
        </Box>
      )}
      {failedAttempts > 0 && (
        <Alert severity="error">
          {t("fields.totpFailedAttempts")}: {failedAttempts}
        </Alert>
      )}
      <Box>
        <Button
          fullWidth
          variant="outlined"
          color="error"
          startIcon={<NoEncryptionOutlinedIcon />}
          onClick={onDisable}
        >
          {t("totp.disable.button")}
        </Button>
      </Box>
    </Stack>
  );
};

type DisabledTotpContentProps = {
  totpSetup: TotpSetup | null;
  totpCode: string;
  totpLoading: boolean;
  totpConfirmLoading: boolean;
  totpError: string | null;
  totpSuccess: boolean;
  onSetup: () => void;
  onTotpCodeChange: (value: string) => void;
  onConfirm: () => void;
  onCopySecret: () => void;
};

export const DisabledTotpContent = ({
  totpSetup,
  totpCode,
  totpLoading,
  totpConfirmLoading,
  totpError,
  totpSuccess,
  onSetup,
  onTotpCodeChange,
  onConfirm,
  onCopySecret,
}: DisabledTotpContentProps) => {
  const { t } = useTranslation("profile");

  return (
    <Stack spacing={2} paddingY={2}>
      <Box>
        <Button
          fullWidth
          variant="contained"
          onClick={onSetup}
          disabled={totpLoading}
        >
          {totpLoading ? (
            <>
              <CircularProgress size={16} sx={{ mr: 1 }} />
              {t("totp.setup.loading")}
            </>
          ) : (
            t("totp.setup.button")
          )}
        </Button>
      </Box>
      {totpError && <Alert severity="error">{totpError}</Alert>}
      {totpSuccess && (
        <Alert severity="success">{t("totp.setup.success")}</Alert>
      )}
      {totpSetup && (
        <TotpSetupForm
          totpSetup={totpSetup}
          totpCode={totpCode}
          totpConfirmLoading={totpConfirmLoading}
          onTotpCodeChange={onTotpCodeChange}
          onConfirm={onConfirm}
          onCopySecret={onCopySecret}
        />
      )}
    </Stack>
  );
};

type DisableTotpDialogProps = {
  open: boolean;
  password: string;
  passwordVisible: boolean;
  loading: boolean;
  error: string | null;
  onClose: () => void;
  onPasswordChange: (value: string) => void;
  onPasswordVisibilityToggle: () => void;
  onConfirm: () => void;
};

export const DisableTotpDialog = ({
  open,
  password,
  passwordVisible,
  loading,
  error,
  onClose,
  onPasswordChange,
  onPasswordVisibilityToggle,
  onConfirm,
}: DisableTotpDialogProps) => {
  const { t } = useTranslation("profile");

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{t("totp.disable.dialogTitle")}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} pt={1}>
          <Typography variant="body2" color="text.secondary">
            {t("totp.disable.dialogDescription")}
          </Typography>
          <TextField
            label={t("totp.disable.passwordLabel")}
            type={passwordVisible ? "text" : "password"}
            value={password}
            onChange={(e) => onPasswordChange(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                onConfirm();
              }
            }}
            fullWidth
            autoFocus
            slotProps={{
              input: {
                endAdornment: (
                  <InputAdornment position="end">
                    <IconButton onClick={onPasswordVisibilityToggle} edge="end">
                      {passwordVisible ? (
                        <VisibilityOffIcon />
                      ) : (
                        <VisibilityIcon />
                      )}
                    </IconButton>
                  </InputAdornment>
                ),
              },
            }}
          />
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={loading}>
          {t("common:actions.cancel")}
        </Button>
        <Button
          color="error"
          variant="contained"
          onClick={onConfirm}
          disabled={loading || !password}
        >
          {loading ? (
            <>
              <CircularProgress size={16} sx={{ mr: 1 }} />
              {t("totp.disable.confirming")}
            </>
          ) : (
            t("totp.disable.confirm")
          )}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
