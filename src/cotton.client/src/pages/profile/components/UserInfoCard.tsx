import {
  Avatar,
  Box,
  Paper,
  Chip,
  Divider,
  Stack,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { UserRole, type User } from "../../../features/auth/types";

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

interface UserInfoCardProps {
  user: User;
}

export const UserInfoCard = ({ user }: UserInfoCardProps) => {
  const { t } = useTranslation("profile");

  const displayName = user.displayName ?? user.username;
  const avatarLetter = displayName.charAt(0).toUpperCase();
  const totpEnabled = Boolean(user.isTotpEnabled);

  return (
    <Paper
      sx={{
        p: { xs: 2, sm: 3 },
        borderRadius: 2,
        border: (theme) => `1px solid ${theme.palette.divider}`,
        flex: { xs: 1, lg: "0 0 400px" },
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

      <Stack direction="row" spacing={1} flexWrap="wrap" sx={{ mb: 3, gap: 1 }}>
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
          <Typography variant="body2" fontWeight={600} textAlign="right">
            {formatDateTime(user.createdAt)}
          </Typography>
        </Box>
      </Stack>
    </Paper>
  );
};
