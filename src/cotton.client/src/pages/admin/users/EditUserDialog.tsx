import React, { useEffect, useState } from "react";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { isAxiosError } from "../../../shared/api/httpClient";
import {
  adminApi,
  type AdminUserDto,
  type AdminUpdateUserRequestDto,
} from "../../../shared/api/adminApi";
import { UserRole } from "../../../features/auth/types";
import { UserRoleSelect } from "./UserRoleSelect";

interface EditUserDialogProps {
  open: boolean;
  user: AdminUserDto | null;
  onClose: () => void;
  onSaved: () => Promise<void>;
}

/**
 * Dialog for editing an existing user's profile.
 */
export const EditUserDialog: React.FC<EditUserDialogProps> = ({
  open,
  user,
  onClose,
  onSaved,
}) => {
  const { t } = useTranslation(["admin", "common"]);

  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<UserRole>(UserRole.User);
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [birthDate, setBirthDate] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!user) return;
    setUsername(user.username);
    setEmail(user.email ?? "");
    setRole(user.role);
    setFirstName(user.firstName ?? "");
    setLastName(user.lastName ?? "");
    setBirthDate(user.birthDate ?? "");
    setError(null);
  }, [user]);

  const canSave = username.trim().length > 0 && !saving;

  const handleSave = async () => {
    if (!user) return;
    setError(null);

    const request: AdminUpdateUserRequestDto = {
      username: username.trim(),
      email: email.trim().length > 0 ? email.trim() : null,
      role,
      firstName: firstName.trim().length > 0 ? firstName.trim() : null,
      lastName: lastName.trim().length > 0 ? lastName.trim() : null,
      birthDate: birthDate.length > 0 ? birthDate : null,
    };

    setSaving(true);
    try {
      await adminApi.updateUser(user.id, request);
      await onSaved();
      onClose();
    } catch (e) {
      if (isAxiosError(e)) {
        const message = (e.response?.data as { message?: string } | undefined)
          ?.message;
        if (typeof message === "string" && message.length > 0) {
          setError(message);
          return;
        }
      }
      setError(t("users.errors.updateFailed"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{t("users.edit.title")}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} pt={1}>
          {error && <Alert severity="error">{error}</Alert>}

          <TextField
            label={t("users.create.username")}
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            fullWidth
            autoComplete="off"
          />
          <TextField
            label={t("users.create.email")}
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            fullWidth
            autoComplete="email"
          />
          <UserRoleSelect
            labelId="admin-edit-user-role-label"
            value={role}
            onChange={setRole}
            disabled={saving}
          />
          <TextField
            label={t("users.create.firstName")}
            value={firstName}
            onChange={(e) => setFirstName(e.target.value)}
            fullWidth
            autoComplete="given-name"
          />
          <TextField
            label={t("users.create.lastName")}
            value={lastName}
            onChange={(e) => setLastName(e.target.value)}
            fullWidth
            autoComplete="family-name"
          />
          <TextField
            label={t("users.create.birthDate")}
            type="date"
            value={birthDate}
            onChange={(e) => setBirthDate(e.target.value)}
            fullWidth
            slotProps={{ inputLabel: { shrink: true } }}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>
          {t("actions.cancel", { ns: "common" })}
        </Button>
        <Button
          variant="contained"
          onClick={handleSave}
          disabled={!canSave}
        >
          {saving ? (
            <Stack direction="row" spacing={1} alignItems="center">
              <CircularProgress size={16} />
              <span>{t("users.edit.saving")}</span>
            </Stack>
          ) : (
            t("users.edit.save")
          )}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
