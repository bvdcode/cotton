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
import { isAxiosError } from "../../../shared/api/httpClient";
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
  const totpEnabledAt = user.totpEnabledAt ?? null;
  const totpFailedAttempts = user.totpFailedAttempts ?? 0;

  const normalizedTotpCode = totpCode.replace(/\D/g, "").slice(0, 6);

  const handleSetupTotp = async () => {
    setTotpError(null);
    setTotpSuccess(false);
    setTotpLoading(true);
    try {
      const setup = await totpApi.setup();
      setTotpSetup(setup);
    } catch (e) {
      if (isAxiosError(e)) {
        const status = e.response?.status;
        const message = (e.response?.data as { message?: string })?.message;
        if (status === 409) {
          setTotpError(t("totp.errors.alreadyEnabled"));
          return;
        }
        if (typeof message === "string" && message.length > 0) {
          setTotpError(message);
          return;
        }
      }
      setTotpError(t("totp.errors.setupFailed"));
    } finally {
      setTotpLoading(false);
    }
  };

  const handleConfirmTotp = async () => {
    setTotpError(null);
    setTotpSuccess(false);
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
      if (isAxiosError(e)) {
        const status = e.response?.status;
        const message = (e.response?.data as { message?: string })?.message;
        if (status === 403) {
          setTotpError(t("totp.errors.invalidCode"));
          return;
        }
        if (status === 400) {
          setTotpError(t("totp.errors.setupNotInitiated"));
          return;
        }
        if (status === 409) {
          setTotpError(t("totp.errors.alreadyEnabled"));
          return;
        }
        if (typeof message === "string" && message.length > 0) {
          setTotpError(message);
          return;
        }
      }
      setTotpError(t("totp.errors.confirmFailed"));
    } finally {
      setTotpConfirmLoading(false);
    }
  };

  const handleCopySecret = async () => {
    if (!totpSetup?.secretBase32) return;
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

  const handleCloseDisableDialog = () => {
    setDisableDialogOpen(false);
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
      if (isAxiosError(e)) {
        const status = e.response?.status;
        if (status === 403) {
          setDisableError(t("totp.errors.invalidPassword"));
          return;
        }
      }
      setDisableError(t("totp.errors.disableFailed"));
    } finally {
      setDisableLoading(false);
    }
  };

  const description = totpEnabled
    ? t("totp.enabledMessage")
    : t("totp.setup.caption");

  return (
    <>
      <ProfileAccordionCard
        id="totp-settings-header"
        ariaControls="totp-settings-content"
        icon={
          totpEnabled ? (
            <SecurityIcon color="primary" />
          ) : (
            <SecurityOutlinedIcon color="primary" />
          )
        }
        title={t("totp.sectionTitle")}
        description={description}
      >
        <Stack spacing={2} paddingY={2}>
          {totpEnabled ? (
            <>
              {totpEnabledAt && (
                <Box display="flex" justifyContent="space-between" gap={2}>
                  <Typography variant="body2" color="text.secondary">
                    {t("fields.totpEnabledAt")}
                  </Typography>
                  <Typography
                    variant="body2"
                    fontWeight={600}
                    textAlign="right"
                  >
                    {formatDateTime(totpEnabledAt)}
                  </Typography>
                </Box>
              )}

              {totpFailedAttempts > 0 && (
                <Alert severity="error">
                  {t("fields.totpFailedAttempts")}: {totpFailedAttempts}
                </Alert>
              )}

              <Box>
                <Button
                  fullWidth
                  variant="outlined"
                  color="error"
                  startIcon={<NoEncryptionOutlinedIcon />}
                  onClick={handleOpenDisableDialog}
                >
                  {t("totp.disable.button")}
                </Button>
              </Box>
            </>
          ) : (
            <>
              <Box>
                <Button
                  fullWidth
                  variant="contained"
                  onClick={handleSetupTotp}
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
                  onTotpCodeChange={setTotpCode}
                  onConfirm={handleConfirmTotp}
                  onCopySecret={handleCopySecret}
                />
              )}
            </>
          )}
        </Stack>
      </ProfileAccordionCard>

      <Dialog
        open={disableDialogOpen}
        onClose={handleCloseDisableDialog}
        maxWidth="xs"
        fullWidth
      >
        <DialogTitle>{t("totp.disable.dialogTitle")}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} pt={1}>
            <Typography variant="body2" color="text.secondary">
              {t("totp.disable.dialogDescription")}
            </Typography>
            <TextField
              label={t("totp.disable.passwordLabel")}
              type={disablePasswordVisible ? "text" : "password"}
              value={disablePassword}
              onChange={(e) => setDisablePassword(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") handleDisableTotp();
              }}
              fullWidth
              autoFocus
              InputProps={{
                endAdornment: (
                  <InputAdornment position="end">
                    <IconButton
                      onClick={() => setDisablePasswordVisible((v) => !v)}
                      edge="end"
                    >
                      {disablePasswordVisible ? (
                        <VisibilityOffIcon />
                      ) : (
                        <VisibilityIcon />
                      )}
                    </IconButton>
                  </InputAdornment>
                ),
              }}
            />
            {disableError && <Alert severity="error">{disableError}</Alert>}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleCloseDisableDialog} disabled={disableLoading}>
            {t("common:actions.cancel")}
          </Button>
          <Button
            color="error"
            variant="contained"
            onClick={handleDisableTotp}
            disabled={disableLoading || !disablePassword}
          >
            {disableLoading ? (
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
    </>
  );
};
