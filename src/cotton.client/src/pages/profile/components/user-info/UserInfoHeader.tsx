import { Cake, Check, Email } from "@mui/icons-material";
import {
  Box,
  Chip,
  CircularProgress,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import type { ChangeEventHandler } from "react";
import { useTranslation } from "react-i18next";
import { UserRole } from "../../../../features/auth/types";
import { AvatarUploadControl } from "./AvatarUploadControl";
import { getRoleTranslationKey } from "./userInfoCardFormatters";

interface UserInfoHeaderProps {
  title: string;
  username: string;
  pictureUrl?: string;
  avatarInitials: string;
  avatarUploadInputId: string;
  avatarUploadInProgress: boolean;
  role: number;
  isTotpEnabled?: boolean;
  email?: string | null;
  isEmailVerified?: boolean;
  birthDateValue: string;
  birthDateCompactValue: string;
  onAvatarFileSelected: ChangeEventHandler<HTMLInputElement>;
  onSendEmailVerification: () => void;
  emailVerificationSending: boolean;
}

interface EmailVerificationActionProps {
  show: boolean;
  loading: boolean;
  onSend: () => void;
  label: string;
}

const EmailVerificationAction = ({
  show,
  loading,
  onSend,
  label,
}: EmailVerificationActionProps) => {
  if (!show) {
    return null;
  }

  return (
    <Box
      sx={{
        display: "grid",
        alignItems: "center",
        justifyItems: "start",
      }}
    >
      <Chip
        size="small"
        color="warning"
        variant="outlined"
        label={label}
        onClick={loading ? undefined : onSend}
        sx={{
          gridArea: "1 / 1",
          opacity: loading ? 0 : 1,
          transform: loading ? "scale(0.9)" : "scale(1)",
          transition: "opacity 220ms ease, transform 220ms ease",
          pointerEvents: loading ? "none" : "auto",
        }}
      />
      <CircularProgress
        size={18}
        color="warning"
        sx={{
          gridArea: "1 / 1",
          opacity: loading ? 1 : 0,
          transform: loading ? "scale(1)" : "scale(0.9)",
          transition: "opacity 220ms ease, transform 220ms ease",
          pointerEvents: "none",
        }}
      />
    </Box>
  );
};

export const UserInfoHeader = ({
  title,
  username,
  pictureUrl,
  avatarInitials,
  avatarUploadInputId,
  avatarUploadInProgress,
  role,
  isTotpEnabled,
  email,
  isEmailVerified,
  birthDateValue,
  birthDateCompactValue,
  onAvatarFileSelected,
  onSendEmailVerification,
  emailVerificationSending,
}: UserInfoHeaderProps) => {
  const { t } = useTranslation(["profile"]);

  const totpEnabled = Boolean(isTotpEnabled);

  return (
    <Stack
      direction={{ xs: "column", sm: "row" }}
      spacing={2.5}
      alignItems={{ xs: "center", sm: "flex-start" }}
    >
      <AvatarUploadControl
        alt={title}
        src={pictureUrl}
        initials={avatarInitials}
        inputId={avatarUploadInputId}
        uploadLabel={t("avatar.upload")}
        isUploading={avatarUploadInProgress}
        onFileSelected={onAvatarFileSelected}
      />

      <Box minWidth={0} flex={1} textAlign={{ xs: "center", sm: "left" }}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={{ xs: 0.75, sm: 1 }}
          alignItems={{ xs: "center", sm: "center" }}
          justifyContent={{ sm: "space-between" }}
        >
          <Box flexGrow={1} minWidth={0}>
            <Typography variant="h5" fontWeight={800} height="100%">
              {title}
            </Typography>
          </Box>

          <Stack
            direction="row"
            spacing={1}
            flexWrap="wrap"
            useFlexGap
            justifyContent={{ xs: "center", sm: "flex-end" }}
          >
            <Chip size="small" label={`@${username}`} />
            <Chip
              size="small"
              color={role === UserRole.Admin ? "secondary" : "primary"}
              label={t(getRoleTranslationKey(role))}
            />
            <Chip
              size="small"
              color={totpEnabled ? "success" : "warning"}
              variant="filled"
              label={totpEnabled ? t("totp.enabled") : t("totp.disabled")}
            />
          </Stack>
        </Stack>

        <Stack
          direction={{ xs: "row", sm: "column" }}
          spacing={{ xs: 1.5, sm: 0.5 }}
          alignItems={{ xs: "center", sm: "flex-start" }}
          justifyContent={{ xs: "center", sm: "flex-start" }}
          mt={0.5}
          sx={{
            minWidth: 0,
            "& > *": {
              minWidth: 0,
            },
          }}
        >
          {birthDateValue && (
            <Box
              display="flex"
              alignItems="center"
              gap={0.5}
              sx={{ order: { xs: 1, sm: 2 }, minWidth: 0 }}
            >
              <Tooltip title={t("fields.birthDate")} placement="left">
                <Cake fontSize="small" />
              </Tooltip>
              <Typography variant="body2" color="text.secondary">
                <Box component="span" sx={{ display: { xs: "inline", sm: "none" } }}>
                  {birthDateCompactValue}
                </Box>
                <Box component="span" sx={{ display: { xs: "none", sm: "inline" } }}>
                  {birthDateValue}
                </Box>
              </Typography>
            </Box>
          )}

          {email && (
            <Stack
              direction="row"
              spacing={1}
              alignItems="center"
              justifyContent={{ xs: "center", sm: "flex-start" }}
              flexWrap={{ xs: "nowrap", sm: "wrap" }}
              useFlexGap
              sx={{ order: { xs: 2, sm: 1 }, minWidth: 0 }}
            >
              <Tooltip title={t("fields.email")} placement="left">
                <Email fontSize="small" />
              </Tooltip>
              <Typography
                variant="body2"
                color="text.secondary"
                noWrap
                sx={{ minWidth: 0 }}
              >
                {email}
              </Typography>
              <EmailVerificationAction
                show={!isEmailVerified}
                loading={emailVerificationSending}
                onSend={onSendEmailVerification}
                label={t("fields.verify")}
              />
              {isEmailVerified && (
                <Tooltip title={t("email.verified")} placement="right">
                  <Check color="success" fontSize="small" />
                </Tooltip>
              )}
            </Stack>
          )}
        </Stack>
      </Box>
    </Stack>
  );
};
