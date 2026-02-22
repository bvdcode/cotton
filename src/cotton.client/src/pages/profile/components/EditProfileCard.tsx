import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Stack,
  TextField,
} from "@mui/material";
import { useState, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { isAxiosError } from "../../../shared/api/httpClient";
import { authApi } from "../../../shared/api/authApi";
import { ProfileAccordionCard } from "./ProfileAccordionCard";
import EditIcon from "@mui/icons-material/Edit";
import type { User } from "../../../features/auth/types";

type EditProfileStatus =
  | { kind: "idle" }
  | { kind: "success" }
  | { kind: "error"; message: string };

interface EditProfileCardProps {
  user: User;
  onUserUpdate: (updatedUser: User) => void;
}

export const EditProfileCard = ({
  user,
  onUserUpdate,
}: EditProfileCardProps) => {
  const { t } = useTranslation("profile");

  const [email, setEmail] = useState(user.email ?? "");
  const [firstName, setFirstName] = useState(user.firstName ?? "");
  const [lastName, setLastName] = useState(user.lastName ?? "");
  const [birthDate, setBirthDate] = useState(user.birthDate ?? "");
  const [loading, setLoading] = useState(false);
  const [status, setStatus] = useState<EditProfileStatus>({ kind: "idle" });

  const emailChanged = useMemo(
    () => (email || null) !== (user.email || null),
    [email, user.email],
  );

  const hasChanges = useMemo(() => {
    return (
      emailChanged ||
      (firstName || null) !== (user.firstName || null) ||
      (lastName || null) !== (user.lastName || null) ||
      (birthDate || null) !== (user.birthDate || null)
    );
  }, [firstName, lastName, birthDate, user, emailChanged]);

  const canSubmit = hasChanges && !loading;

  const handleSubmit = useCallback(async () => {
    setStatus({ kind: "idle" });
    setLoading(true);

    try {
      const updated = await authApi.updateProfile({
        email: email || null,
        firstName: firstName || null,
        lastName: lastName || null,
        birthDate: birthDate || null,
      });
      onUserUpdate(updated);
      setStatus({ kind: "success" });
    } catch (e) {
      if (isAxiosError(e)) {
        const data = e.response?.data as
          | { message?: string; title?: string }
          | undefined;
        const message = data?.message ?? data?.title;
        if (message) {
          setStatus({ kind: "error", message });
          return;
        }
      }
      setStatus({ kind: "error", message: t("editProfile.errors.failed") });
    } finally {
      setLoading(false);
    }
  }, [email, firstName, lastName, birthDate, onUserUpdate, t]);

  return (
    <ProfileAccordionCard
      id="edit-profile-header"
      ariaControls="edit-profile-content"
      icon={<EditIcon color="primary" />}
      title={t("editProfile.title")}
      description={t("editProfile.description")}
    >
      <Stack spacing={2} paddingY={2}>
        {status.kind === "success" && (
          <Alert severity="success">{t("editProfile.success")}</Alert>
        )}
        {status.kind === "error" && (
          <Alert severity="error">{status.message}</Alert>
        )}

        {emailChanged && (
          <Alert severity="warning">{t("editProfile.emailWarning")}</Alert>
        )}

        <Box display="flex" flexDirection={{ xs: "column", sm: "row" }} gap={2}>
          <TextField
            label={t("editProfile.firstName")}
            value={firstName}
            onChange={(e) => setFirstName(e.target.value)}
            fullWidth
          />

          <TextField
            label={t("editProfile.lastName")}
            value={lastName}
            onChange={(e) => setLastName(e.target.value)}
            fullWidth
          />
        </Box>

        <Box
          display="flex"
          flexDirection={{ xs: "column", sm: "row" }}
          gap={2}
          alignItems="center"
        >
          <TextField
            label={t("editProfile.email")}
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            fullWidth
          />

          <TextField
            label={t("editProfile.birthDate")}
            type="date"
            value={birthDate}
            onChange={(e) => setBirthDate(e.target.value)}
            fullWidth
            slotProps={{
              inputLabel: { shrink: true },
            }}
          />
        </Box>

        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={!canSubmit}
          startIcon={loading ? <CircularProgress size={18} /> : undefined}
        >
          {loading ? t("editProfile.saving") : t("editProfile.save")}
        </Button>
      </Stack>
    </ProfileAccordionCard>
  );
};
