import {
  Avatar,
  Box,
  Card,
  CardContent,
  Chip,
  Divider,
  Stack,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { useAuth } from "../../features/auth";
import { UserRole } from "../../features/auth/types";

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
  const { user } = useAuth();

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

  return (
    <Box sx={{ p: 2, maxWidth: 900 }}>
      <Typography variant="h5" fontWeight={700}>
        {t("title")}
      </Typography>
      <Typography color="text.secondary" sx={{ mt: 0.5, mb: 2 }}>
        {t("subtitle")}
      </Typography>

      <Card variant="outlined" sx={{ borderRadius: 2 }}>
        <CardContent>
          <Stack direction="row" spacing={2} alignItems="center">
            <Avatar
              alt={displayName}
              src={user.pictureUrl}
              sx={{ width: 56, height: 56, bgcolor: "primary.main" }}
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

              <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                <Chip
                  size="small"
                  color={user.role === UserRole.Admin ? "warning" : "default"}
                  label={t(getRoleTranslationKey(user.role))}
                />
                <Chip
                  size="small"
                  color={totpEnabled ? "success" : "default"}
                  variant={totpEnabled ? "filled" : "outlined"}
                  label={totpEnabled ? t("totp.enabled") : t("totp.disabled")}
                />
              </Stack>
            </Box>
          </Stack>

          <Divider sx={{ my: 2 }} />

          <Stack spacing={1.25}>
            <Box display="flex" justifyContent="space-between" gap={2}>
              <Typography variant="body2" color="text.secondary">
                {t("fields.role")}
              </Typography>
              <Typography variant="body2" fontWeight={600}>
                {t(getRoleTranslationKey(user.role))}
              </Typography>
            </Box>

            <Box display="flex" justifyContent="space-between" gap={2}>
              <Typography variant="body2" color="text.secondary">
                {t("fields.twoFactor")}
              </Typography>
              <Typography variant="body2" fontWeight={600}>
                {totpEnabled ? t("totp.enabled") : t("totp.disabled")}
              </Typography>
            </Box>

            {totpEnabled && totpEnabledAt && (
              <Box display="flex" justifyContent="space-between" gap={2}>
                <Typography variant="body2" color="text.secondary">
                  {t("fields.totpEnabledAt")}
                </Typography>
                <Typography variant="body2" fontWeight={600}>
                  {formatDateTime(totpEnabledAt)}
                </Typography>
              </Box>
            )}

            <Box display="flex" justifyContent="space-between" gap={2}>
              <Typography variant="body2" color="text.secondary">
                {t("fields.totpFailedAttempts")}
              </Typography>
              <Typography variant="body2" fontWeight={600}>
                {totpFailedAttempts}
              </Typography>
            </Box>
          </Stack>

          {/* Future: TOTP setup UI will be mounted here when disabled */}
        </CardContent>
      </Card>
    </Box>
  );
};
