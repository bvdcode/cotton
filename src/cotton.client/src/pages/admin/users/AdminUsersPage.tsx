import {
  Alert,
  Button,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  DataGrid,
  type GridColDef,
  GridActionsCellItem,
} from "@mui/x-data-grid";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { isAxiosError } from "../../../shared/api/httpClient";
import {
  adminApi,
  type AdminUserDto,
} from "../../../shared/api/adminApi";
import { UserRole } from "../../../features/auth/types";
import { CreateUserForm } from "./CreateUserForm";
import { EditUserDialog } from "./EditUserDialog";

type LoadState =
  | { kind: "idle" }
  | { kind: "loading" }
  | { kind: "error"; message: string };

const formatDateTime = (iso: string | null): string => {
  if (!iso) return "";
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
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
  const [loadState, setLoadState] = useState<LoadState>({ kind: "loading" });
  const [editingUser, setEditingUser] = useState<AdminUserDto | null>(null);

  const roleLabel = useMemo(() => {
    return (r: UserRole) => {
      if (r === UserRole.Admin) return t("roles.admin");
      if (r === UserRole.User) return t("roles.user");
      return t("roles.unknown");
    };
  }, [t]);

  const fetchUsers = useCallback(async () => {
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
  }, [t]);

  const isLoading = loadState.kind === "loading";

  const columns: GridColDef<AdminUserDto>[] = useMemo(
    () => [
      {
        field: "username",
        headerName: t("users.columns.username"),
        flex: 1,
        minWidth: 120,
      },
      {
        field: "email",
        headerName: t("users.columns.email"),
        flex: 1,
        minWidth: 120,
        valueGetter: (_, row) => row.email ?? "",
        sortable: false,
      },
      {
        field: "role",
        headerName: t("users.columns.role"),
        width: 130,
        valueGetter: (_, row) => roleLabel(row.role),
        sortable: false,
      },
      {
        field: "firstName",
        headerName: t("users.columns.firstName"),
        flex: 1,
        minWidth: 100,
        valueGetter: (_, row) => row.firstName ?? "",
        sortable: false,
      },
      {
        field: "lastName",
        headerName: t("users.columns.lastName"),
        flex: 1,
        minWidth: 100,
        valueGetter: (_, row) => row.lastName ?? "",
        sortable: false,
      },
      {
        field: "isTotpEnabled",
        headerName: t("users.columns.totp"),
        width: 80,
        valueGetter: (_, row) =>
          row.isTotpEnabled
            ? t("yes", { ns: "common" })
            : t("no", { ns: "common" }),
        sortable: false,
      },
      {
        field: "activeSessionCount",
        headerName: t("users.columns.sessions"),
        width: 100,
        type: "number",
      },
      {
        field: "lastActivityAt",
        headerName: t("users.columns.lastActivity"),
        flex: 1,
        minWidth: 140,
        valueGetter: (_, row) => formatDateTime(row.lastActivityAt),
        sortable: false,
      },
      {
        field: "actions",
        headerName: t("users.columns.actions"),
        type: "actions",
        width: 80,
        getActions: (params) => [
          <GridActionsCellItem
            key="edit"
            icon={<EditOutlinedIcon />}
            label={t("users.actions.edit")}
            onClick={() => setEditingUser(params.row)}
          />,
        ],
      },
    ],
    [roleLabel, t],
  );

  useEffect(() => {
    let cancelled = false;

    adminApi.getUsers().then((result) => {
      if (!cancelled) {
        setUsers(result);
        setLoadState({ kind: "idle" });
      }
    }).catch((e: unknown) => {
      if (cancelled) return;
      if (isAxiosError(e)) {
        const message = (e.response?.data as { message?: string } | undefined)
          ?.message;
        if (typeof message === "string" && message.length > 0) {
          setLoadState({ kind: "error", message });
          return;
        }
      }
      setLoadState({ kind: "error", message: t("users.errors.loadFailed") });
    });

    return () => { cancelled = true; };
  }, [t]);

  return (
    <Stack spacing={2}>
      <CreateUserForm onUserCreated={fetchUsers} />

      <Paper>
        <Stack spacing={2} p={2}>
          <Stack
            direction="row"
            justifyContent="space-between"
            alignItems="center"
          >
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

          <DataGrid
            rows={users}
            columns={columns}
            getRowId={(row) => row.id}
            loading={isLoading}
            disableColumnMenu
            disableColumnFilter
            disableRowSelectionOnClick
            hideFooter
            autoHeight
          />
        </Stack>
      </Paper>

      <EditUserDialog
        open={editingUser !== null}
        user={editingUser}
        onClose={() => setEditingUser(null)}
        onSaved={fetchUsers}
      />
    </Stack>
  );
};
