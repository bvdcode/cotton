import { useState, useCallback } from "react";
import { Box, Button, Typography, Alert, Chip, Stack } from "@mui/material";
import { MarkEmailRead, MarkEmailUnread } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { User } from "../../../features/auth/types";
import { ProfileAccordionCard } from "./ProfileAccordionCard";
import { authApi } from "../../../shared/api/authApi";
import { isAxiosError } from "../../../shared/api/httpClient";

type ProblemDetails = {
  title?: string;
  detail?: string;
  message?: string;
  errors?: Record<string, string | string[]>;
};

const tryGetProblemDetailsMessage = (data: ProblemDetails | undefined): string | null => {
  const direct = data?.message ?? data?.detail ?? data?.title;
  if (typeof direct === "string" && direct.trim().length > 0) {
    return direct;
  }

  const errors = data?.errors;
  if (!errors) return null;

  for (const value of Object.values(errors)) {
    if (typeof value === "string" && value.trim().length > 0) {
      return value;
    }
    if (Array.isArray(value)) {
      const first = value.find((x) => typeof x === "string" && x.trim().length > 0);
      if (first) return first;
    }
  }

  return null;
};

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
    } catch (e) {
      if (isAxiosError(e)) {
        const data = e.response?.data as ProblemDetails | undefined;
        const message = tryGetProblemDetailsMessage(data);
        if (message) {
          setError(message);
          return;
        }
      }
      setError(t("emailVerification.errors.failed"));
    } finally {
      setSending(false);
    }
  }, [t]);

  return (
    <ProfileAccordionCard
      id="email-verification-header"
      ariaControls="email-verification-content"
      icon={
        isVerified ? (
          <MarkEmailRead color="success" />
        ) : (
          <MarkEmailUnread color="warning" />
        )
      }
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
