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
import { useState } from "react";
import {
  getApiErrorMessage,
  isAxiosError,
} from "../../../shared/api/httpClient";
import { totpApi, type TotpSetup } from "../../../shared/api/totpApi";
import { authApi } from "../../../shared/api/authApi";
import type { User } from "../../../features/auth/types";
import { TotpSetupForm } from "./TotpSetupForm";
import { ProfileAccordionCard } from "./ProfileAccordionCard";
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

interface TotpSettingsCardProps {
  user: User;
  onUserUpdate: (user: User) => void;
}

export const TotpSettingsCard = ({
  user,
  onUserUpdate,
}: TotpSettingsCardProps) => {
  const { t } = useTranslation("profile");

  const [totpSetup, setTotpSetup] = useState<TotpSetup | null>(null);
  const [totpLoading, setTotpLoading] = useState(false);
  const [totpConfirmLoading, setTotpConfirmLoading] = useState(false);
  const [totpCode, setTotpCode] = useState("");
  const [totpError, setTotpError] = useState<string | null>(null);
  const [totpSuccess, setTotpSuccess] = useState(false);

  const [disableDialogOpen, setDisableDialogOpen] = useState(false);
  const [disablePassword, setDisablePassword] = useState("");
  const [disablePasswordVisible, setDisablePasswordVisible] = useState(false);
  const [disableLoading, setDisableLoading] = useState(false);
  const [disableError, setDisableError] = useState<string | null>(null);

  const totpEnabled = Boolean(user.isTotpEnabled);
  const description = totpEnabled
    ? t("totp.enabledMessage")
    : t("totp.setup.caption");

  const handleSetupTotp = async () => {
    setTotpError(null);
    setTotpSuccess(false);
    setTotpLoading(true);
    try {
      const setup = await totpApi.setup();
      setTotpSetup(setup);
    } catch (e) {
      setTotpError(resolveTotpSetupError(e, t));
    } finally {
      setTotpLoading(false);
    }
  };

  const handleConfirmTotp = async () => {
    setTotpError(null);
    setTotpSuccess(false);
    const normalizedTotpCode = totpCode.replace(/\D/g, "").slice(0, 6);
    if (normalizedTotpCode.length < 6) {
      setTotpError(t("totp.errors.codeRequired"));
      return;
    }

    setTotpConfirmLoading(true);
    try {
      await totpApi.confirm(normalizedTotpCode);
      const refreshed = await authApi.me();
      onUserUpdate(refreshed);
      setTotpSuccess(true);
      setTotpSetup(null);
      setTotpCode("");
    } catch (e) {
      setTotpError(resolveTotpConfirmError(e, t));
    } finally {
      setTotpConfirmLoading(false);
    }
  };

  const handleCopySecret = async () => {
    if (!totpSetup?.secretBase32) {
      return;
    }

    try {
      await navigator.clipboard.writeText(totpSetup.secretBase32);
    } catch {
      // ignore clipboard errors
    }
  };

  const handleOpenDisableDialog = () => {
    setDisablePassword("");
    setDisableError(null);
    setDisablePasswordVisible(false);
    setDisableDialogOpen(true);
  };

  const handleDisableTotp = async () => {
    setDisableError(null);
    if (!disablePassword) {
      setDisableError(t("totp.errors.invalidPassword"));
      return;
    }

    setDisableLoading(true);
    try {
      await totpApi.disable(disablePassword);
      const refreshed = await authApi.me();
      onUserUpdate(refreshed);
      setDisableDialogOpen(false);
      setTotpSuccess(false);
      setTotpError(null);
    } catch (e) {
      setDisableError(resolveTotpDisableError(e, t));
    } finally {
      setDisableLoading(false);
    }
  };

  return (
    <>
      <ProfileAccordionCard
        id="totp-settings-header"
        ariaControls="totp-settings-content"
        icon={<TotpStatusIcon enabled={totpEnabled} />}
        title={t("totp.sectionTitle")}
        description={description}
      >
        {totpEnabled ? (
          <EnabledTotpContent
            enabledAt={user.totpEnabledAt ?? null}
            failedAttempts={user.totpFailedAttempts ?? 0}
            onDisable={handleOpenDisableDialog}
          />
        ) : (
          <DisabledTotpContent
            totpSetup={totpSetup}
            totpCode={totpCode}
            totpLoading={totpLoading}
            totpConfirmLoading={totpConfirmLoading}
            totpError={totpError}
            totpSuccess={totpSuccess}
            onSetup={handleSetupTotp}
            onTotpCodeChange={setTotpCode}
            onConfirm={handleConfirmTotp}
            onCopySecret={handleCopySecret}
          />
        )}
      </ProfileAccordionCard>
      <DisableTotpDialog
        open={disableDialogOpen}
        password={disablePassword}
        passwordVisible={disablePasswordVisible}
        loading={disableLoading}
        error={disableError}
        onClose={() => setDisableDialogOpen(false)}
        onPasswordChange={setDisablePassword}
        onPasswordVisibilityToggle={() => setDisablePasswordVisible((v) => !v)}
        onConfirm={handleDisableTotp}
      />
    </>
  );
};

type Translate = ReturnType<typeof useTranslation>["t"];

const resolveTotpSetupError = (error: unknown, t: Translate) => {
  if (isAxiosError(error) && error.response?.status === 409) {
    return t("totp.errors.alreadyEnabled");
  }

  return getApiErrorMessage(error) ?? t("totp.errors.setupFailed");
};

const resolveTotpConfirmError = (error: unknown, t: Translate) => {
  if (isAxiosError(error)) {
    const statusMessage = getConfirmStatusMessage(error.response?.status, t);
    if (statusMessage) {
      return statusMessage;
    }
  }

  return getApiErrorMessage(error) ?? t("totp.errors.confirmFailed");
};

const getConfirmStatusMessage = (status: number | undefined, t: Translate) => {
  switch (status) {
    case 403:
      return t("totp.errors.invalidCode");
    case 400:
      return t("totp.errors.setupNotInitiated");
    case 409:
      return t("totp.errors.alreadyEnabled");
    default:
      return null;
  }
};

const resolveTotpDisableError = (error: unknown, t: Translate) => {
  if (isAxiosError(error) && error.response?.status === 403) {
    return t("totp.errors.invalidPassword");
  }

  return getApiErrorMessage(error) ?? t("totp.errors.disableFailed");
};

type TotpStatusIconProps = {
  enabled: boolean;
};

const TotpStatusIcon = ({ enabled }: TotpStatusIconProps) =>
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

const EnabledTotpContent = ({
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

const DisabledTotpContent = ({
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
        <Button fullWidth variant="contained" onClick={onSetup} disabled={totpLoading}>
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
      {totpSuccess && <Alert severity="success">{t("totp.setup.success")}</Alert>}
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

const DisableTotpDialog = ({
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
                      {passwordVisible ? <VisibilityOffIcon /> : <VisibilityIcon />}
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
