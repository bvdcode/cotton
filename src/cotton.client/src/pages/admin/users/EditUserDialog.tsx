import React, { useState } from "react";
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
import { getApiErrorMessage } from "../../../shared/api/httpClient";
import {
  type AdminUserDto,
  type AdminUpdateUserRequestDto,
} from "../../../shared/api/adminApi";
import { useUpdateAdminUserMutation } from "../../../shared/api/queries/admin";
import { UserRole } from "../../../features/auth/types";
import { UserRoleSelect } from "./UserRoleSelect";
import { UserPersonalInfoFields } from "./UserPersonalInfoFields";
import { toDateInputValue } from "../../../shared/utils/dateOnly";
import {
  getUsernameError,
  isValidUsername,
  normalizeUsername,
} from "../../../shared/validation/username";

interface EditUserDialogProps {
  open: boolean;
  user: AdminUserDto | null;
  onClose: () => void;
}

export const EditUserDialog: React.FC<EditUserDialogProps> = ({
  open,
  user,
  onClose,
}) => {
  if (!open || !user) {
    return null;
  }

  return (
    <EditUserDialogContent key={user.id} user={user} onClose={onClose} />
  );
};

interface EditUserDialogContentProps {
  user: AdminUserDto;
  onClose: () => void;
}

const EditUserDialogContent: React.FC<EditUserDialogContentProps> = ({
  user,
  onClose,
}) => {
  const { t } = useTranslation(["admin", "common"]);

  const [username, setUsername] = useState(user.username);
  const [email, setEmail] = useState(user.email ?? "");
  const [role, setRole] = useState<UserRole>(user.role);
  const [firstName, setFirstName] = useState(user.firstName ?? "");
  const [lastName, setLastName] = useState(user.lastName ?? "");
  const [birthDate, setBirthDate] = useState(toDateInputValue(user.birthDate));
  const [error, setError] = useState<string | null>(null);
  const updateUserMutation = useUpdateAdminUserMutation();
  const saving = updateUserMutation.isPending;

  const usernameError = getUsernameError(username);
  const usernameValid = isValidUsername(username);

  const canSave = usernameValid && !saving;

  const handleSave = async () => {
    setError(null);

    const request: AdminUpdateUserRequestDto = {
      username: normalizeUsername(username),
      email: email.trim().length > 0 ? email.trim() : null,
      role,
      firstName: firstName.trim().length > 0 ? firstName.trim() : null,
      lastName: lastName.trim().length > 0 ? lastName.trim() : null,
      birthDate: birthDate.length > 0 ? birthDate : null,
    };

    try {
      await updateUserMutation.mutateAsync({ userId: user.id, request });
      onClose();
    } catch (e) {
      const message = getApiErrorMessage(e);
      if (message) {
        setError(message);
        return;
      }

      setError(t("users.errors.updateFailed"));
    }
  };

  const handleClose = () => {
    if (!saving) {
      onClose();
    }
  };

  return (
    <Dialog open onClose={handleClose} maxWidth="sm" fullWidth>
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
            error={Boolean(usernameError)}
            disabled={saving}
          />
          <TextField
            label={t("users.create.email")}
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            fullWidth
            autoComplete="email"
            disabled={saving}
          />
          <UserRoleSelect
            labelId="admin-edit-user-role-label"
            value={role}
            onChange={setRole}
            disabled={saving}
          />
          <UserPersonalInfoFields
            firstName={firstName}
            lastName={lastName}
            birthDate={birthDate}
            onFirstNameChange={setFirstName}
            onLastNameChange={setLastName}
            onBirthDateChange={setBirthDate}
            disabled={saving}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose} disabled={saving}>
          {t("actions.cancel", { ns: "common" })}
        </Button>
        <Button
          variant="contained"
          onClick={handleSave}
          disabled={!canSave}
        >
          {saving ? (
            <Stack direction="row" spacing={1} alignItems="center">
              <CircularProgress size={16} color="inherit" />
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
