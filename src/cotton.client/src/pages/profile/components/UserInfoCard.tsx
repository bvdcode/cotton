import {
  Alert,
  Avatar,
  Box,
  CircularProgress,
  Paper,
  Chip,
  Divider,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import PhotoCameraIcon from "@mui/icons-material/PhotoCamera";
import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import { UserRole, type User } from "../../../features/auth/types";
import { authApi } from "../../../shared/api/authApi";
import { isAxiosError } from "../../../shared/api/httpClient";
import { useSettingsStore } from "../../../shared/store/settingsStore";
import { uploadBlobToChunks } from "../../../shared/upload";
import {
  formatDateOnly,
  getAgeYears,
  tryParseDateOnly,
} from "../../../shared/utils/dateOnly";
import { formatBytes } from "../../../shared/utils/formatBytes";

type AvatarStatus = { kind: "idle" } | { kind: "error"; message: string };

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
  onUserUpdate: (updatedUser: User) => void;
}

type InfoRowProps = {
  label: string;
  value: React.ReactNode;
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

export const UserInfoCard = ({ user, onUserUpdate }: UserInfoCardProps) => {
  const { t } = useTranslation(["profile", "common"]);
  const avatarUploadInputId = "profile-avatar-upload-input";
  const [avatarUploading, setAvatarUploading] = useState(false);
  const [avatarStatus, setAvatarStatus] = useState<AvatarStatus>({ kind: "idle" });

  const serverSettings = useSettingsStore((state) => state.data);
  const fetchServerSettings = useSettingsStore((state) => state.fetchSettings);

  const getAvatarInitials = (args: {
    firstName?: string | null;
    lastName?: string | null;
    username?: string | null;
    email?: string | null;
  }): string => {
    const first = (args.firstName ?? "").trim();
    const last = (args.lastName ?? "").trim();
    if (first && last) {
      return `${first.charAt(0)}${last.charAt(0)}`.toUpperCase();
    }

    const fallback = (args.username ?? args.email ?? "").trim();
    if (!fallback) return "";
    return fallback.slice(0, 2).toUpperCase();
  };

  const totpEnabled = Boolean(user.isTotpEnabled);
  const placeholder = t("common:placeholder");

  const fullName = [user.firstName, user.lastName]
    .filter((p): p is string => typeof p === "string" && p.trim().length > 0)
    .join(" ");

  // title is full name if present, otherwise username
  const title = fullName || user.username;
  const displayName = title; // for avatar alt
  const avatarInitials = getAvatarInitials({
    firstName: user.firstName,
    lastName: user.lastName,
    username: user.username,
    email: user.email,
  });

  const birthDateValue = (() => {
    if (!user.birthDate || user.birthDate.trim().length === 0) {
      return placeholder;
    }

    const formatted = formatDateOnly(user.birthDate);
    const parsed = tryParseDateOnly(user.birthDate);
    if (!parsed) {
      return formatted;
    }

    const ageYears = getAgeYears(parsed);
    if (ageYears < 0 || ageYears > 150) {
      return formatted;
    }

    return `${formatted} (${t("ageYears", { count: ageYears })})`;
  })();

  const handleAvatarFileSelected = useCallback(
    async (event: React.ChangeEvent<HTMLInputElement>): Promise<void> => {
      const selectedFile = event.target.files?.[0];
      event.target.value = "";

      if (!selectedFile || avatarUploading) {
        return;
      }

      setAvatarStatus({ kind: "idle" });
      setAvatarUploading(true);

      try {
        let effectiveServerSettings = serverSettings;
        if (!effectiveServerSettings) {
          await fetchServerSettings({ force: false });
          effectiveServerSettings = useSettingsStore.getState().data;
        }

        if (!effectiveServerSettings) {
          setAvatarStatus({ kind: "error", message: t("avatar.errors.settingsNotLoaded") });
          return;
        }

        if (selectedFile.size > effectiveServerSettings.maxChunkSizeBytes) {
          setAvatarStatus({
            kind: "error",
            message: t("avatar.errors.fileTooLarge", {
              maxSize: formatBytes(effectiveServerSettings.maxChunkSizeBytes),
            }),
          });
          return;
        }

        const { chunkHashes } = await uploadBlobToChunks({
          blob: selectedFile,
          fileName: selectedFile.name,
          server: {
            maxChunkSizeBytes: effectiveServerSettings.maxChunkSizeBytes,
            supportedHashAlgorithm: effectiveServerSettings.supportedHashAlgorithm,
          },
        });

        if (chunkHashes.length !== 1) {
          setAvatarStatus({
            kind: "error",
            message: t("avatar.errors.fileTooLarge", {
              maxSize: formatBytes(effectiveServerSettings.maxChunkSizeBytes),
            }),
          });
          return;
        }

        const updatedUser = await authApi.updateProfile({
          avatarHash: chunkHashes[0],
        });

        onUserUpdate(updatedUser);
      } catch (error) {
        if (isAxiosError(error)) {
          const data = error.response?.data as
            | { message?: string; title?: string }
            | undefined;
          const message = data?.message ?? data?.title;
          if (message) {
            setAvatarStatus({ kind: "error", message });
            return;
          }
        }

        setAvatarStatus({ kind: "error", message: t("avatar.errors.failed") });
      } finally {
        setAvatarUploading(false);
      }
    },
    [avatarUploading, fetchServerSettings, onUserUpdate, serverSettings, t],
  );

  return (
    <Paper
      sx={{
        display: "flex",
        flexDirection: "column",
        p: { xs: 2, sm: 3 },
      }}
    >
      {avatarStatus.kind === "error" && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {avatarStatus.message}
        </Alert>
      )}

      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={2.5}
        alignItems={{ xs: "center", sm: "flex-start" }}
        sx={{ mb: 3 }}
      >
        <Box
          sx={{
            position: "relative",
            width: { xs: 84, sm: 104 },
            height: { xs: 84, sm: 104 },
            borderRadius: "50%",
            overflow: "hidden",
            cursor: "pointer",
            "&:hover .avatar-upload-overlay": {
              transform: "translateY(0)",
            },
          }}
        >
          <Avatar
            alt={displayName}
            src={user.pictureUrl}
            sx={{
              width: "100%",
              height: "100%",
              bgcolor: "primary.main",
            }}
          >
            {!user.pictureUrl && avatarInitials}
          </Avatar>

          {avatarUploading ? (
            <Box
              sx={{
                position: "absolute",
                inset: 0,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                color: "common.white",
                bgcolor: "rgba(0, 0, 0, 0.58)",
              }}
            >
              <CircularProgress size={28} sx={{ color: "common.white" }} />
            </Box>
          ) : (
            <Box
              className="avatar-upload-overlay"
              component="label"
              htmlFor={avatarUploadInputId}
              aria-label={t("avatar.upload")}
              sx={{
                position: "absolute",
                bottom: 0,
                left: 0,
                right: 0,
                height: "50%",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                color: "common.white",
                bgcolor: "rgba(0, 0, 0, 0.58)",
                cursor: "pointer",
                transform: "translateY(100%)",
                transition: "transform 0.2s ease, background-color 0.2s ease",
                "&:hover": {
                  bgcolor: "rgba(0, 0, 0, 0.72)",
                },
              }}
            >
              <PhotoCameraIcon fontSize="small" />
            </Box>
          )}

          <input
            id={avatarUploadInputId}
            type="file"
            accept=".bmp,.gif,.jpeg,.jpg,.pbm,.png,.tiff,.tif,.tga,.webp,.qoi"
            hidden
            disabled={avatarUploading}
            onChange={handleAvatarFileSelected}
          />
        </Box>

        <Box minWidth={0} flex={1} textAlign={{ xs: "center", sm: "left" }}>
          <Stack
            direction="row"
            spacing={1}
            alignItems="center"
            justifyContent={{ xs: "center", sm: "space-between" }}
            flexWrap="wrap"
            useFlexGap
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
          </Stack>
          {user.email && (
            <Typography
              variant="body2"
              color="text.secondary"
              noWrap
              display={{ xs: "none", sm: "block" }}
            >
              {user.email}
            </Typography>
          )}
        </Box>
      </Stack>

      <Divider sx={{ mb: 2 }} />

      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={{ xs: 1.5, sm: 3 }}
      >
        <Stack spacing={1.25} flex={1}>
          <InfoRow
            label={t("fields.username")}
            value={
              <Tooltip title={user.id} arrow>
                <Box component="span">{user.username}</Box>
              </Tooltip>
            }
          />
          <InfoRow
            label={t("fields.email")}
            value={
              user.email && user.email.trim().length > 0
                ? user.email
                : placeholder
            }
          />
        </Stack>

        <Stack spacing={1.25} flex={1}>
          <InfoRow label={t("fields.birthDate")} value={birthDateValue} />
          <InfoRow
            label={t("fields.createdAt")}
            value={formatDateTime(user.createdAt)}
          />
        </Stack>
      </Stack>
    </Paper>
  );
};
