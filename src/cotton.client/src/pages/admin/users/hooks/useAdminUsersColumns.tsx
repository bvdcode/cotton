import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Box, Chip, CircularProgress, Tooltip, Typography } from "@mui/material";
import {
  GridActionsCellItem,
  type GridColDef,
} from "@mui/x-data-grid";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CancelIcon from "@mui/icons-material/Cancel";
import type { AdminUserDto } from "../../../../shared/api/adminApi";
import { UserRole } from "../../../../features/auth/types";
import { formatDateOnly } from "../../../../shared/utils/dateOnly";
import { formatTimeAgo } from "../../../../shared/utils/formatTimeAgo";
import { formatStorageBytes } from "../utils/formatStorageBytes";

const renderOptionalText = (value: string) =>
  value ? (
    value
  ) : (
    <Box component="span" sx={{ color: "text.disabled" }}>
      —
    </Box>
  );

interface UseAdminUsersColumnsOptions {
  storageUsageLoading: boolean;
  onEdit: (user: AdminUserDto) => void;
}

export const useAdminUsersColumns = ({
  storageUsageLoading,
  onEdit,
}: UseAdminUsersColumnsOptions): GridColDef<AdminUserDto>[] => {
  const { t } = useTranslation(["admin", "common"]);

  const roleLabel = useMemo(() => {
    return (r: UserRole) => {
      if (r === UserRole.Admin) return t("roles.admin");
      if (r === UserRole.User) return t("roles.user");
      return t("roles.unknown");
    };
  }, [t]);

  const storageUsageCalculatingLabel = t("users.storageUsage.calculating");

  return useMemo(
    () => [
      {
        field: "username",
        headerName: t("users.columns.username"),
        flex: 1,
        minWidth: 100,
      },
      {
        field: "email",
        headerName: t("users.columns.email"),
        flex: 1,
        minWidth: 120,
        valueGetter: (_, row) => row.email ?? "",
        renderCell: (params) => renderOptionalText(String(params.value ?? "")),
        sortable: false,
      },
      {
        field: "role",
        headerName: t("users.columns.role"),
        minWidth: 100,
        renderCell: (params) => {
          const role = params.row.role;
          const isAdmin = role === UserRole.Admin;
          return (
            <Chip
              label={roleLabel(role)}
              color={isAdmin ? "primary" : "default"}
              variant="outlined"
              size="small"
            />
          );
        },
        sortable: false,
      },
      {
        field: "firstName",
        headerName: t("users.columns.firstName"),
        flex: 1,
        minWidth: 100,
        valueGetter: (_, row) => row.firstName ?? "",
        renderCell: (params) => renderOptionalText(String(params.value ?? "")),
        sortable: false,
      },
      {
        field: "lastName",
        headerName: t("users.columns.lastName"),
        flex: 1,
        minWidth: 100,
        valueGetter: (_, row) => row.lastName ?? "",
        renderCell: (params) => renderOptionalText(String(params.value ?? "")),
        sortable: false,
      },
      {
        field: "birthDate",
        headerName: t("users.columns.birthDate"),
        flex: 1,
        minWidth: 130,
        valueGetter: (_, row) =>
          row.birthDate ? formatDateOnly(row.birthDate) : "",
        renderCell: (params) => renderOptionalText(String(params.value ?? "")),
        sortable: false,
      },
      {
        field: "isTotpEnabled",
        headerName: t("users.columns.totp"),
        width: 60,
        renderCell: (params) => (
          <Box
            height="100%"
            width="100%"
            display="flex"
            alignItems="center"
            justifyContent="center"
          >
            {params.row.isTotpEnabled ? (
              <CheckCircleIcon sx={{ color: "success.main" }} />
            ) : (
              <CancelIcon sx={{ color: "error.main" }} />
            )}
          </Box>
        ),
        sortable: false,
      },
      {
        field: "activeSessionCount",
        headerName: t("users.columns.sessions"),
        width: 80,
        type: "number",
      },
      {
        field: "storageUsedBytes",
        headerName: t("users.columns.storageUsed"),
        width: 120,
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
            {storageUsageLoading ? (
              <CircularProgress
                aria-label={storageUsageCalculatingLabel}
                size={16}
                thickness={5}
              />
            ) : (
              <Typography
                variant="body2"
                fontWeight={600}
                sx={{ fontVariantNumeric: "tabular-nums" }}
              >
                {formatStorageBytes(params.row.storageUsedBytes)}
              </Typography>
            )}
          </Box>
        ),
      },
      {
        field: "lastActivityAt",
        headerName: t("users.columns.lastActivity"),
        flex: 1,
        minWidth: 140,
        renderCell: (params) => {
          const iso = params.row.lastActivityAt;
          if (!iso) return "";
          const date = new Date(iso);
          const localDateTime = new Intl.DateTimeFormat(undefined, {
            year: "numeric",
            month: "short",
            day: "2-digit",
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
          }).format(date);
          return (
            <Tooltip title={localDateTime}>
              <span>{formatTimeAgo(iso, t)}</span>
            </Tooltip>
          );
        },
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
            onClick={() => onEdit(params.row)}
          />,
        ],
      },
    ],
    [onEdit, roleLabel, storageUsageCalculatingLabel, storageUsageLoading, t],
  );
};
