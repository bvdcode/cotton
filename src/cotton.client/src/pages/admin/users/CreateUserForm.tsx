import React, { useState } from "react";
import {
  Alert,
  Button,
  CircularProgress,
  Paper,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { isAxiosError } from "../../../shared/api/httpClient";
import {
  adminApi,
  type AdminCreateUserRequestDto,
} from "../../../shared/api/adminApi";
import { UserRole } from "../../../features/auth/types";
import { UserRoleSelect } from "./UserRoleSelect";
import { UserPersonalInfoFields } from "./UserPersonalInfoFields";
import {
  getUsernameError,
  isValidUsername,
  normalizeUsername,
} from "../../../shared/validation/username";

interface CreateUserFormProps {
  onUserCreated: () => Promise<void>;
}

export const CreateUserForm: React.FC<CreateUserFormProps> = ({
  onUserCreated,
}) => {
  const { t } = useTranslation(["admin", "common"]);

  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState<UserRole>(UserRole.User);
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [birthDate, setBirthDate] = useState("");
  const [createLoading, setCreateLoading] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);
  const [createSuccess, setCreateSuccess] = useState(false);

  const usernameError = getUsernameError(username);
  const usernameValid = isValidUsername(username);

  const canCreate =
    usernameValid && password.length > 0 && !createLoading;

  const handleCreate = async () => {
    setCreateError(null);
    setCreateSuccess(false);

    const request: AdminCreateUserRequestDto = {
      username: normalizeUsername(username),
      email: email.trim().length > 0 ? email.trim() : null,
      password,
      role,
      firstName: firstName.trim().length > 0 ? firstName.trim() : null,
      lastName: lastName.trim().length > 0 ? lastName.trim() : null,
      birthDate: birthDate.length > 0 ? birthDate : null,
    };

    setCreateLoading(true);
    try {
      await adminApi.createUser(request);
      setCreateSuccess(true);
      setUsername("");
      setEmail("");
      setPassword("");
      setRole(UserRole.User);
      setFirstName("");
      setLastName("");
      setBirthDate("");
      await onUserCreated();
    } catch (e) {
      if (isAxiosError(e)) {
        const message = (e.response?.data as { message?: string } | undefined)
          ?.message;
        if (typeof message === "string" && message.length > 0) {
          setCreateError(message);
          return;
        }
      }
      setCreateError(t("users.errors.createFailed"));
    } finally {
      setCreateLoading(false);
    }
  };

  return (
    <Paper>
      <Stack spacing={2} p={2}>
        <Typography variant="h6" fontWeight={700}>
          {t("users.create.title")}
        </Typography>

        {createSuccess && (
          <Alert severity="success">{t("users.create.success")}</Alert>
        )}
        {createError && <Alert severity="error">{createError}</Alert>}

        <Stack
          sx={{
            display: "grid",
            gridTemplateColumns: {
              xs: "1fr",
              sm: "repeat(auto-fit, 250px)",
            },
            gap: 2,
            justifyContent: "start",
            alignItems: "start",
          }}
        >
          <TextField
            label={t("users.create.username")}
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            fullWidth
            autoComplete="off"
            error={Boolean(usernameError)}
            helperText={usernameError ?? ""}
          />
          <TextField
            label={t("users.create.email")}
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            fullWidth
            autoComplete="email"
          />
          <TextField
            label={t("users.create.password")}
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            fullWidth
            autoComplete="new-password"
          />
          <UserRoleSelect
            labelId="admin-create-user-role-label"
            value={role}
            onChange={setRole}
            disabled={createLoading}
          />
          <UserPersonalInfoFields
            firstName={firstName}
            lastName={lastName}
            birthDate={birthDate}
            onFirstNameChange={setFirstName}
            onLastNameChange={setLastName}
            onBirthDateChange={setBirthDate}
            disabled={createLoading}
          />
        </Stack>

        <Stack direction="row" justifyContent="flex-end">
          <Button
            variant="contained"
            onClick={handleCreate}
            disabled={!canCreate}
          >
            {createLoading ? (
              <Stack direction="row" spacing={1} alignItems="center">
                <CircularProgress size={16} />
                <span>{t("users.create.creating")}</span>
              </Stack>
            ) : (
              t("users.create.button")
            )}
          </Button>
        </Stack>
      </Stack>
    </Paper>
  );
};

