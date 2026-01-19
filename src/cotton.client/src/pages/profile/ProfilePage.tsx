import {
  Avatar,
  Box,
  Paper,
  Chip,
  Button,
  Alert,
  Divider,
  CircularProgress,
  Stack,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { useAuth } from "../../features/auth";
import { UserRole } from "../../features/auth/types";
import { useState } from "react";
import QRCode from "react-qr-code";
import { totpApi, type TotpSetup } from "../../shared/api/totpApi";
import { OneTimeCodeInput } from "../../shared/ui/OneTimeCodeInput";
import axios from "axios";
import { authApi } from "../../shared/api/authApi";

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

const getRoleTranslationKey = (role: number): "roles.user" | "roles.admin" => {
  return role === UserRole.Admin ? "roles.admin" : "roles.user";
};

export const ProfilePage = () => {
  const { t } = useTranslation("profile");
  const { user, setAuthenticated } = useAuth();

  const [totpSetup, setTotpSetup] = useState<TotpSetup | null>(null);
  const [totpLoading, setTotpLoading] = useState(false);
  const [totpConfirmLoading, setTotpConfirmLoading] = useState(false);
  const [totpCode, setTotpCode] = useState("");
  const [totpError, setTotpError] = useState<string | null>(null);
  const [totpSuccess, setTotpSuccess] = useState(false);

  if (!user) {
    return (
      <Box sx={{ p: 2 }}>
        <Typography variant="h5" fontWeight={700}>
          {t("title")}
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>
          {t("notAuthenticated")}
        </Typography>
      </Box>
    );
  }

  const displayName = user.displayName ?? user.username;
  const avatarLetter = displayName.charAt(0).toUpperCase();

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
      if (axios.isAxiosError(e)) {
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
      setAuthenticated(true, refreshed);
      setTotpSuccess(true);
      setTotpSetup(null);
      setTotpCode("");
    } catch (e) {
      if (axios.isAxiosError(e)) {
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

  return (
    <Box sx={{ p: 2, maxWidth: 1400 }}>
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" },
          gap: 3,
        }}
      >
        <Paper
          elevation={0}
          sx={{
            p: 3,
            borderRadius: 2,
            border: (theme) => `1px solid ${theme.palette.divider}`,
          }}
        >
          <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 3 }}>
            <Avatar
              alt={displayName}
              src={user.pictureUrl}
              sx={{ width: 64, height: 64, bgcolor: "primary.main" }}
            >
              {!user.pictureUrl && avatarLetter}
            </Avatar>

            <Box sx={{ minWidth: 0, flex: 1 }}>
              <Typography variant="h6" fontWeight={700} noWrap>
                {displayName}
              </Typography>
              <Typography variant="body2" color="text.secondary" noWrap>
                @{user.username}
              </Typography>
            </Box>
          </Stack>

          <Stack direction="row" spacing={1} sx={{ mb: 3 }}>
            <Chip
              size="small"
              color={user.role === UserRole.Admin ? "warning" : "default"}
              label={t(getRoleTranslationKey(user.role))}
            />
            <Chip
              size="small"
              color={totpEnabled ? "success" : "warning"}
              variant="filled"
              label={totpEnabled ? t("totp.enabled") : t("totp.disabled")}
            />
          </Stack>

          <Divider sx={{ mb: 2 }} />

          <Stack spacing={1.5}>
            <Box display="flex" justifyContent="space-between" gap={2}>
              <Typography variant="body2" color="text.secondary">
                {t("fields.createdAt")}
              </Typography>
              <Typography variant="body2" fontWeight={600}>
                {formatDateTime(user.createdAt)}
              </Typography>
            </Box>
          </Stack>
        </Paper>

        {/* Правая колонка: Двухфакторная аутентификация */}
        <Paper
          elevation={0}
          sx={{
            p: 3,
            borderRadius: 2,
            border: (theme) => `1px solid ${theme.palette.divider}`,
          }}
        >
          <Typography variant="h6" fontWeight={700} gutterBottom>
            {t("totp.sectionTitle")}
          </Typography>

          {totpEnabled ? (
            <Box>
              <Alert severity="success" sx={{ mb: 2 }}>
                {t("totp.enabledMessage")}
              </Alert>
              
              {totpEnabledAt && (
                <Box display="flex" justifyContent="space-between" gap={2} sx={{ mb: 2 }}>
                  <Typography variant="body2" color="text.secondary">
                    {t("fields.totpEnabledAt")}
                  </Typography>
                  <Typography variant="body2" fontWeight={600}>
                    {formatDateTime(totpEnabledAt)}
                  </Typography>
                </Box>
              )}
              
              {totpFailedAttempts > 0 && (
                <Alert severity="error" sx={{ mb: 2 }}>
                  {t("fields.totpFailedAttempts")}: {totpFailedAttempts}
                </Alert>
              )}
              
              <Typography variant="body2" color="text.secondary">
                {t("totp.enabledDescription")}
              </Typography>
            </Box>
          ) : (
            <Box>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                {t("totp.setup.caption")}
              </Typography>

              <Stack
                direction={{ xs: "column", sm: "row" }}
                spacing={1}
                sx={{ mb: 2 }}
              >
                <Button
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

                {totpSetup?.secretBase32 && (
                  <Button variant="outlined" onClick={handleCopySecret}>
                    {t("totp.setup.copySecret")}
                  </Button>
                )}
              </Stack>

              {totpError && (
                <Alert severity="error" sx={{ mb: 2 }}>
                  {totpError}
                </Alert>
              )}
              {totpSuccess && (
                <Alert severity="success" sx={{ mb: 2 }}>
                  {t("totp.setup.success")}
                </Alert>
              )}

              {totpSetup && (
                <Box>
                  <Divider sx={{ my: 2 }} />

                  <Typography variant="body2" fontWeight={600} gutterBottom>
                    {t("totp.setup.qrTitle")}
                  </Typography>

                  <Box
                    sx={{
                      mt: 2,
                      mb: 2,
                      p: 2,
                      borderRadius: 2,
                      display: "inline-flex",
                      bgcolor: "#fff",
                      border: (theme) => `1px solid ${theme.palette.divider}`,
                    }}
                  >
                    <QRCode value={totpSetup.otpAuthUri} size={180} />
                  </Box>

                  <Typography
                    variant="caption"
                    color="text.secondary"
                    sx={{ display: "block", mb: 3, wordBreak: "break-all" }}
                  >
                    {t("totp.setup.secretLabel")}:{" "}
                    <strong>{totpSetup.secretBase32}</strong>
                  </Typography>

                  <Divider sx={{ mb: 2 }} />

                  <Typography variant="body2" fontWeight={600} gutterBottom>
                    {t("totp.confirm.title")}
                  </Typography>
                  <Typography
                    variant="caption"
                    color="text.secondary"
                    sx={{ display: "block", mb: 2 }}
                  >
                    {t("totp.confirm.caption")}
                  </Typography>

                  <Box sx={{ mb: 2 }}>
                    <OneTimeCodeInput
                      value={totpCode}
                      onChange={setTotpCode}
                      disabled={totpConfirmLoading}
                      autoFocus={false}
                      inputAriaLabel={t("totp.confirm.digit")}
                    />
                  </Box>

                  <Button
                    variant="contained"
                    onClick={handleConfirmTotp}
                    disabled={totpConfirmLoading}
                  >
                    {totpConfirmLoading ? (
                      <>
                        <CircularProgress size={16} sx={{ mr: 1 }} />
                        {t("totp.confirm.loading")}
                      </>
                    ) : (
                      t("totp.confirm.button")
                    )}
                  </Button>
                </Box>
              )}
            </Box>
          )}
        </Paper>
      </Box>
    </Box>
  );
};
