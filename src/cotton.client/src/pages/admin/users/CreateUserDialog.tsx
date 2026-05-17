import {
  Alert,
  Box,
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
import { UserRoleSelect } from "./UserRoleSelect";
import { UserRole } from "../../../features/auth/types";
import React, { useCallback, useState } from "react";
import { getApiErrorMessage } from "../../../shared/api/httpClient";
import { type AdminCreateUserRequestDto } from "../../../shared/api/adminApi";
import { useCreateAdminUserMutation } from "../../../shared/api/queries/admin";
import {
  getUsernameError,
  isValidUsername,
  normalizeUsername,
} from "../../../shared/validation/username";

interface CreateUserDialogProps {
  open: boolean;
  onClose: () => void;
}

export const CreateUserDialog: React.FC<CreateUserDialogProps> = ({
  open,
  onClose,
}) => {
  if (!open) {
    return null;
  }

  return <CreateUserDialogContent onClose={onClose} />;
};

interface CreateUserDialogContentProps {
  onClose: () => void;
}

const CreateUserDialogContent: React.FC<CreateUserDialogContentProps> = ({
  onClose,
}) => {
  const { t } = useTranslation(["admin", "common"]);

  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState<UserRole>(UserRole.User);
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [birthDate, setBirthDate] = useState("");
  const [createError, setCreateError] = useState<string | null>(null);
  const createUserMutation = useCreateAdminUserMutation();
  const createLoading = createUserMutation.isPending;

  const resetForm = useCallback(() => {
    setUsername("");
    setEmail("");
    setPassword("");
    setRole(UserRole.User);
    setFirstName("");
    setLastName("");
    setBirthDate("");
    setCreateError(null);
  }, []);

  const usernameError = getUsernameError(username);
  const canCreate = isValidUsername(username) && !createLoading;

  const handleClose = () => {
    if (!createLoading) {
      resetForm();
      onClose();
    }
  };

  const handleCreate = async () => {
    setCreateError(null);

    const request: AdminCreateUserRequestDto = {
      username: normalizeUsername(username),
      email: email.trim().length > 0 ? email.trim() : null,
      password: password.trim().length > 0 ? password : null,
      role,
      firstName: firstName.trim().length > 0 ? firstName.trim() : null,
      lastName: lastName.trim().length > 0 ? lastName.trim() : null,
      birthDate: birthDate.length > 0 ? birthDate : null,
    };

    try {
      await createUserMutation.mutateAsync(request);
      resetForm();
      onClose();
    } catch (error) {
      const message = getApiErrorMessage(error);
      if (message) {
        setCreateError(message);
        return;
      }

      setCreateError(t("users.errors.createFailed"));
    }
  };

  return (
    <Dialog
      open
      onClose={handleClose}
      maxWidth="sm"
      fullWidth
      PaperProps={{
        sx: {
          width: "100%",
          maxWidth: 680,
        },
      }}
    >
      <DialogTitle>{t("users.create.title")}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          {createError && <Alert severity="error">{createError}</Alert>}
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "minmax(0, 1fr)",
                sm: "repeat(2, minmax(0, 1fr))",
              },
              gap: 2,
            }}
          >
            <TextField
              label={t("users.create.username")}
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              autoFocus
              autoComplete="off"
              fullWidth
              error={Boolean(usernameError)}
              disabled={createLoading}
            />

            <UserRoleSelect
              labelId="admin-create-user-role-label"
              value={role}
              onChange={setRole}
              disabled={createLoading}
            />

            <TextField
              label={t("users.create.email")}
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="email"
              fullWidth
              disabled={createLoading}
            />

            <TextField
              label={t("users.create.password")}
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="new-password"
              fullWidth
              disabled={createLoading}
            />

            <TextField
              label={t("users.create.firstName")}
              value={firstName}
              onChange={(event) => setFirstName(event.target.value)}
              autoComplete="given-name"
              fullWidth
              disabled={createLoading}
            />

            <TextField
              label={t("users.create.lastName")}
              value={lastName}
              onChange={(event) => setLastName(event.target.value)}
              autoComplete="family-name"
              fullWidth
              disabled={createLoading}
            />

            <TextField
              label={t("users.create.birthDate")}
              type="date"
              value={birthDate}
              onChange={(event) => setBirthDate(event.target.value)}
              fullWidth
              disabled={createLoading}
              slotProps={{ inputLabel: { shrink: true } }}
              sx={{ gridColumn: { xs: "auto", sm: "1 / -1" } }}
            />
          </Box>
        </Stack>
      </DialogContent>

      <DialogActions>
        <Button onClick={handleClose} disabled={createLoading}>
          {t("actions.cancel", { ns: "common" })}
        </Button>

        <Button
          variant="contained"
          onClick={handleCreate}
          disabled={!canCreate}
        >
          {createLoading ? (
            <Stack direction="row" spacing={1} alignItems="center">
              <CircularProgress size={16} color="inherit" />
              <span>{t("users.create.creating")}</span>
            </Stack>
          ) : (
            t("users.create.button")
          )}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
