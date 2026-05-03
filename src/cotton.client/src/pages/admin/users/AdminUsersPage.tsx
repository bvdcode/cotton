import { Alert, Badge, Box, Stack, Tooltip, Typography } from "@mui/material";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  ColumnsPanelTrigger,
  DataGrid,
  FilterPanelTrigger,
  ToolbarButton,
  GridActionsCellItem,
  type GridColDef,
  useGridApiContext,
  useGridRootProps,
} from "@mui/x-data-grid";
import { GridToolbar } from "@mui/x-data-grid/internals";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import PersonAddAltIcon from "@mui/icons-material/PersonAddAlt";
import RefreshIcon from "@mui/icons-material/Refresh";
import { isAxiosError } from "../../../shared/api/httpClient";
import { adminApi, type AdminUserDto } from "../../../shared/api/adminApi";
import { UserRole } from "../../../features/auth/types";
import { CreateUserDialog } from "./CreateUserDialog";
import { EditUserDialog } from "./EditUserDialog";
import { formatDateOnly } from "../../../shared/utils/dateOnly";

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

const formatStorageBytes = (bytes: number): string => {
  if (!Number.isFinite(bytes) || bytes <= 0) return "0 B";

  const units = ["B", "KB", "MB", "GB", "TB", "PB"];
  let value = bytes;
  let unitIndex = 0;

  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }

  const fractionDigits = unitIndex === 0 ? 0 : 2;
  return `${new Intl.NumberFormat(undefined, {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(value)} ${units[unitIndex]}`;
};

interface UsersGridToolbarProps {
  createLabel: string;
  loading: boolean;
  refreshLabel: string;
  onCreate: () => void;
  onRefresh: () => void;
}

const UsersGridToolbar = ({
  createLabel,
  loading,
  refreshLabel,
  onCreate,
  onRefresh,
}: UsersGridToolbarProps) => {
  const apiRef = useGridApiContext();
  const rootProps = useGridRootProps();
  const ColumnSelectorIcon = rootProps.slots.columnSelectorIcon;
  const FilterIcon = rootProps.slots.openFilterButtonIcon;

  return (
    <GridToolbar
      mainControls={
        <>
          <Tooltip title={refreshLabel}>
            <span>
              <ToolbarButton
                aria-label={refreshLabel}
                onClick={onRefresh}
                disabled={loading}
              >
                <RefreshIcon fontSize="small" />
              </ToolbarButton>
            </span>
          </Tooltip>

          <Tooltip title={createLabel}>
            <ToolbarButton aria-label={createLabel} onClick={onCreate}>
              <PersonAddAltIcon fontSize="small" />
            </ToolbarButton>
          </Tooltip>

          {!rootProps.disableColumnSelector && (
            <Tooltip title={apiRef.current.getLocaleText("toolbarColumns")}>
              <ColumnsPanelTrigger render={<ToolbarButton />}>
                <ColumnSelectorIcon fontSize="small" />
              </ColumnsPanelTrigger>
            </Tooltip>
          )}

          {!rootProps.disableColumnFilter && (
            <Tooltip title={apiRef.current.getLocaleText("toolbarFilters")}>
              <FilterPanelTrigger
                render={(triggerProps, state) => (
                  <ToolbarButton
                    {...triggerProps}
                    color={state.filterCount > 0 ? "primary" : "default"}
                  >
                    <Badge
                      badgeContent={state.filterCount}
                      color="primary"
                      variant="dot"
                    >
                      <FilterIcon fontSize="small" />
                    </Badge>
                  </ToolbarButton>
                )}
              />
            </Tooltip>
          )}
        </>
      }
    />
  );
};

export const AdminUsersPage = () => {
  const { t } = useTranslation(["admin", "common"]);

  const [users, setUsers] = useState<AdminUserDto[]>([]);
  const [loadState, setLoadState] = useState<LoadState>({ kind: "loading" });
  const [editingUser, setEditingUser] = useState<AdminUserDto | null>(null);
  const [createOpen, setCreateOpen] = useState(false);

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
  const createLabel = t("users.create.button");
  const refreshLabel = t("users.refresh");

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
        field: "birthDate",
        headerName: t("users.columns.birthDate"),
        flex: 1,
        minWidth: 130,
        valueGetter: (_, row) =>
          row.birthDate ? formatDateOnly(row.birthDate) : "",
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
        field: "storageUsedBytes",
        headerName: t("users.columns.storageUsed"),
        width: 135,
        type: "number",
        align: "right",
        headerAlign: "right",
        renderCell: (params) => (
          <Box
            height="100%"
            width="100%"
            display="flex"
            alignItems="center"
            justifyContent="flex-end"
          >
            <Typography
              variant="body2"
              fontWeight={600}
              sx={{ fontVariantNumeric: "tabular-nums" }}
            >
              {formatStorageBytes(params.row.storageUsedBytes)}
            </Typography>
          </Box>
        ),
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

    adminApi
      .getUsers()
      .then((result) => {
        if (!cancelled) {
          setUsers(result);
          setLoadState({ kind: "idle" });
        }
      })
      .catch((e: unknown) => {
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

    return () => {
      cancelled = true;
    };
  }, [t]);

  const GridToolbarSlot = useMemo(() => {
    const ToolbarSlot = () => (
      <UsersGridToolbar
        createLabel={createLabel}
        loading={isLoading}
        refreshLabel={refreshLabel}
        onCreate={() => setCreateOpen(true)}
        onRefresh={() => void fetchUsers()}
      />
    );

    return ToolbarSlot;
  }, [createLabel, fetchUsers, isLoading, refreshLabel]);

  return (
    <Stack spacing={1} height="100%" minHeight={0}>
      {loadState.kind === "error" && (
        <Alert severity="error">{loadState.message}</Alert>
      )}

      <DataGrid
        rows={users}
        columns={columns}
        getRowId={(row) => row.id}
        loading={isLoading}
        disableRowSelectionOnClick
        showToolbar
        label={t("users.title")}
        pageSizeOptions={[10, 25, 50, 100]}
        initialState={{
          pagination: {
            paginationModel: {
              pageSize: 25,
              page: 0,
            },
          },
        }}
        pagination
        slots={{ toolbar: GridToolbarSlot }}
        sx={{
          flex: 1,
          minHeight: 0,
          "& .MuiDataGrid-toolbar": {
            px: 1,
            py: 0.75,
          },
        }}
      />

      <CreateUserDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onUserCreated={fetchUsers}
      />
      <EditUserDialog
        open={editingUser !== null}
        user={editingUser}
        onClose={() => setEditingUser(null)}
        onSaved={fetchUsers}
      />
    </Stack>
  );
};
