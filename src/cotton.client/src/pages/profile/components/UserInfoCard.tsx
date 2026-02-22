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

const getRoleTranslationKey = (
  role: number,
): "roles.user" | "roles.admin" | "roles.unknown" => {
  switch (role) {
    case UserRole.Admin:
      return "roles.admin";
    case UserRole.User:
      return "roles.user";
    default:
      return "roles.unknown";
  }
};

interface UserInfoCardProps {
  user: User;
}

type InfoRowProps = {
  label: string;
  value: string;
};

const InfoRow = ({ label, value }: InfoRowProps) => {
  return (
    <Box display="flex" justifyContent="space-between" gap={2}>
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
      <Typography
        variant="body2"
        fontWeight={600}
        textAlign="right"
        sx={{ wordBreak: "break-word" }}
      >
        {value}
      </Typography>
    </Box>
  );
};

export const UserInfoCard = ({ user }: UserInfoCardProps) => {
  const { t } = useTranslation(["profile", "common"]);

  const displayName = user.displayName ?? user.username;
  const avatarLetter = displayName.charAt(0).toUpperCase();
  const totpEnabled = Boolean(user.isTotpEnabled);
  const placeholder = t("common:placeholder");

  const fullName = [user.firstName, user.lastName]
    .filter((p): p is string => typeof p === "string" && p.trim().length > 0)
    .join(" ");

  const title = fullName || displayName;

  const subtitleParts: string[] = [];
  if (title !== displayName) {
    subtitleParts.push(displayName);
  }
  subtitleParts.push(`@${user.username}`);
  const subtitle = subtitleParts.join(" · ");

  const formatDate = (iso: string): string => {
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) {
      return iso;
    }
    return new Intl.DateTimeFormat(undefined, {
      year: "numeric",
      month: "short",
      day: "2-digit",
    }).format(date);
  };

  return (
    <Paper
      sx={{
        display: "flex",
        flexDirection: "column",
        p: { xs: 2, sm: 3 },
      }}
    >
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 3 }}>
        <Avatar
          alt={displayName}
          src={user.pictureUrl}
          sx={{ width: 64, height: 64, bgcolor: "primary.main" }}
        >
          {!user.pictureUrl && avatarLetter}
        </Avatar>

        <Box minWidth={0} flex={1}>
          <Typography variant="h6" fontWeight={700} noWrap>
            {title}
          </Typography>
          <Typography variant="body2" color="text.secondary" noWrap>
            {subtitle}
          </Typography>
          {user.email && (
            <Typography variant="body2" color="text.secondary" noWrap>
              {user.email}
            </Typography>
          )}
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
        {user.email && (
          <Chip
            size="small"
            color={user.isEmailVerified ? "success" : "warning"}
            variant="filled"
            label={
              user.isEmailVerified
                ? t("fields.emailVerified")
                : t("fields.emailNotVerified")
            }
          />
        )}
      </Stack>

      <Divider sx={{ mb: 2 }} />

      <Stack direction={{ xs: "column", sm: "row" }} spacing={{ xs: 1.5, sm: 3 }}>
        <Stack spacing={1.25} flex={1}>
          <InfoRow label={t("fields.username")} value={user.username} />
          <InfoRow
            label={t("fields.email")}
            value={user.email && user.email.trim().length > 0 ? user.email : placeholder}
          />
          <InfoRow
            label={t("fields.firstName")}
            value={
              user.firstName && user.firstName.trim().length > 0
                ? user.firstName
                : placeholder
            }
          />
          <InfoRow
            label={t("fields.lastName")}
            value={
              user.lastName && user.lastName.trim().length > 0
                ? user.lastName
                : placeholder
            }
          />
        </Stack>

        <Stack spacing={1.25} flex={1}>
          <InfoRow
            label={t("fields.birthDate")}
            value={
              user.birthDate && user.birthDate.trim().length > 0
                ? formatDate(user.birthDate)
                : placeholder
            }
          />
          <InfoRow label={t("fields.createdAt")} value={formatDateTime(user.createdAt)} />
          <InfoRow label={t("fields.updatedAt")} value={formatDateTime(user.updatedAt)} />
          <InfoRow label={t("fields.id")} value={user.id} />
        </Stack>
      </Stack>
    </Paper>
  );
};
