import {
  Alert,
  Button,
  CircularProgress,
  FormControl,
  InputLabel,
  LinearProgress,
  MenuItem,
  Paper,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { isAxiosError } from "../../../shared/api/httpClient";
import {
  adminApi,
  type AdminCreateUserRequestDto,
  type AdminUserDto,
} from "../../../shared/api/adminApi";
import { UserRole } from "../../../features/auth/types";

type LoadState =
  | { kind: "idle" }
  | { kind: "loading" }
  | { kind: "error"; message: string };

const formatDateTime = (iso: string | null): string => {
  if (!iso) return "";
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

export const AdminUsersPage = () => {
  const { t } = useTranslation(["admin", "common"]);

  const [users, setUsers] = useState<AdminUserDto[]>([]);
  const [loadState, setLoadState] = useState<LoadState>({ kind: "idle" });

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState<UserRole>(UserRole.User);
  const [createLoading, setCreateLoading] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);
  const [createSuccess, setCreateSuccess] = useState(false);

  const canCreate =
    username.trim().length > 0 && password.length > 0 && !createLoading;

  const roleLabel = useMemo(() => {
    return (r: UserRole) => {
      if (r === UserRole.Admin) return t("roles.admin");
      if (r === UserRole.User) return t("roles.user");
      return t("roles.unknown");
    };
  }, [t]);

  const fetchUsers = async () => {
    setLoadState({ kind: "loading" });
    try {
      const result = await adminApi.getUsers();
      setUsers(result);
      setLoadState({ kind: "idle" });
    } catch (e) {
      if (isAxiosError(e)) {
        const message = (e.response?.data as { message?: string } | undefined)
          ?.message;
        if (typeof message === "string" && message.length > 0) {
          setLoadState({ kind: "error", message });
          return;
        }
      }
      setLoadState({ kind: "error", message: t("users.errors.loadFailed") });
    }
  };

  const isLoading = loadState.kind === "loading";

  useEffect(() => {
    void fetchUsers();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleCreate = async () => {
    setCreateError(null);
    setCreateSuccess(false);

    const request: AdminCreateUserRequestDto = {
      username: username.trim(),
      password,
      role,
    };

    setCreateLoading(true);
    try {
      await adminApi.createUser(request);
      setCreateSuccess(true);
      setUsername("");
      setPassword("");
      setRole(UserRole.User);
      await fetchUsers();
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
    <Stack spacing={2}>
      <Paper>
        <Stack spacing={1} p={2}>
          <Stack direction="row" justifyContent="space-between" alignItems="center">
            <Typography variant="h6" fontWeight={700}>
              {t("users.title")}
            </Typography>
            <Button
              variant="outlined"
              onClick={() => fetchUsers()}
              disabled={isLoading}
            >
              {t("users.refresh")}
            </Button>
          </Stack>

          {loadState.kind === "error" && (
            <Alert severity="error">{loadState.message}</Alert>
          )}

          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell colSpan={5} sx={{ p: 0 }}>
                  <LinearProgress
                    sx={{
                      height: 3,
                      borderRadius: 0,
                      visibility: isLoading ? "visible" : "hidden",
                    }}
                  />
                </TableCell>
              </TableRow>
              <TableRow>
                <TableCell>{t("users.columns.username")}</TableCell>
                <TableCell>{t("users.columns.role")}</TableCell>
                <TableCell>{t("users.columns.totp")}</TableCell>
                <TableCell align="right">{t("users.columns.sessions")}</TableCell>
                <TableCell>{t("users.columns.lastActivity")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {users.map((u) => (
                <TableRow key={u.id} hover>
                  <TableCell>{u.username}</TableCell>
                  <TableCell>{roleLabel(u.role)}</TableCell>
                  <TableCell>
                    {u.isTotpEnabled
                      ? t("yes", { ns: "common" })
                      : t("no", { ns: "common" })}
                  </TableCell>
                  <TableCell align="right">{u.activeSessionCount}</TableCell>
                  <TableCell>{formatDateTime(u.lastActivityAt)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Stack>
      </Paper>

      <Paper>
        <Stack spacing={2} p={2}>
          <Typography variant="h6" fontWeight={700}>
            {t("users.create.title")}
          </Typography>

          {createSuccess && (
            <Alert severity="success">{t("users.create.success")}</Alert>
          )}
          {createError && <Alert severity="error">{createError}</Alert>}

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              label={t("users.create.username")}
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              fullWidth
              autoComplete="off"
            />
            <TextField
              label={t("users.create.password")}
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              fullWidth
              autoComplete="new-password"
            />
            <FormControl fullWidth>
              <InputLabel id="admin-user-role-label">
                {t("users.create.role")}
              </InputLabel>
              <Select
                labelId="admin-user-role-label"
                label={t("users.create.role")}
                value={role}
                onChange={(e) => setRole(e.target.value as UserRole)}
              >
                <MenuItem value={UserRole.User}>{t("roles.user")}</MenuItem>
                <MenuItem value={UserRole.Admin}>{t("roles.admin")}</MenuItem>
              </Select>
            </FormControl>
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
    </Stack>
  );
};
