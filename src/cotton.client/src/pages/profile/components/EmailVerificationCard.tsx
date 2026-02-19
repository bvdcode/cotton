import { useState, useCallback } from "react";
import { Box, Button, Typography, Alert, Chip, Stack } from "@mui/material";
import { MarkEmailRead, MarkEmailUnread } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { User } from "../../../features/auth/types";
import { ProfileAccordionCard } from "./ProfileAccordionCard";
import { authApi } from "../../../shared/api/authApi";

interface EmailVerificationCardProps {
  user: User;
}

export const EmailVerificationCard = ({ user }: EmailVerificationCardProps) => {
  const { t } = useTranslation("profile");
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState("");

  const isVerified = Boolean(user.isEmailVerified);
  const hasEmail = Boolean(user.email);

  const handleSendVerification = useCallback(async () => {
    setError("");
    setSent(false);
    setSending(true);
    try {
      await authApi.sendEmailVerification();
      setSent(true);
    } catch {
      setError(t("emailVerification.errors.failed"));
    } finally {
      setSending(false);
    }
  }, [t]);

  return (
    <ProfileAccordionCard
      id="email-verification-header"
      ariaControls="email-verification-content"
      icon={isVerified ? <MarkEmailRead /> : <MarkEmailUnread />}
      title={t("emailVerification.title")}
      description={t("emailVerification.description")}
    >
      <Stack spacing={2}>
        <Box display="flex" alignItems="center" gap={1}>
          <Chip
            size="small"
            color={isVerified ? "success" : "warning"}
            label={
              isVerified
                ? t("emailVerification.verified")
                : t("emailVerification.notVerified")
            }
          />
          {user.email && (
            <Typography variant="body2" color="text.secondary">
              {user.email}
            </Typography>
          )}
        </Box>

        {!isVerified && !hasEmail && (
          <Alert severity="info">{t("emailVerification.noEmail")}</Alert>
        )}

        {!isVerified && hasEmail && !sent && (
          <Button
            variant="outlined"
            onClick={handleSendVerification}
            disabled={sending}
          >
            {sending
              ? t("emailVerification.sending")
              : t("emailVerification.sendButton")}
          </Button>
        )}

        {sent && (
          <Alert severity="success">{t("emailVerification.sent")}</Alert>
        )}

        {error && <Alert severity="error">{error}</Alert>}
      </Stack>
    </ProfileAccordionCard>
  );
};
