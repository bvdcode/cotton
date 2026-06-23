import { Box, Chip, Stack, Tooltip, Typography } from "@mui/material";
import { GridActionsCellItem, type GridColDef } from "@mui/x-data-grid";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import type { OidcProviderDto } from "@shared/api/oidcApi";

interface UseOidcProviderColumnsOptions {
  onEdit: (provider: OidcProviderDto) => void;
  onDelete: (provider: OidcProviderDto) => void;
}

const joinList = (values: readonly string[]): string =>
  values.length > 0 ? values.join(", ") : "-";

export const useOidcProviderColumns = ({
  onEdit,
  onDelete,
}: UseOidcProviderColumnsOptions): GridColDef<OidcProviderDto>[] => {
  const { t } = useTranslation(["admin", "common"]);

  return useMemo(
    () => [
      {
        field: "name",
        headerName: t("identityProviders.columns.name"),
        flex: 1,
        minWidth: 160,
        renderCell: (params) => (
          <Stack spacing={0} justifyContent="center" height="100%" minWidth={0}>
            <Typography fontWeight={600} noWrap>
              {params.row.name}
            </Typography>
            <Typography variant="body2" color="text.secondary" noWrap>
              {params.row.slug}
            </Typography>
          </Stack>
        ),
      },
      {
        field: "issuer",
        headerName: t("identityProviders.columns.issuer"),
        flex: 1.2,
        minWidth: 220,
      },
      {
        field: "isEnabled",
        headerName: t("identityProviders.columns.status"),
        width: 140,
        renderCell: (params) => (
          <Box height="100%" display="flex" alignItems="center">
            <Chip
              size="small"
              color={params.row.isEnabled ? "success" : "default"}
              label={
                params.row.isEnabled
                  ? t("identityProviders.status.enabled")
                  : t("identityProviders.status.disabled")
              }
            />
          </Box>
        ),
      },
      {
        field: "allowAccountCreation",
        headerName: t("identityProviders.columns.accountCreation"),
        width: 150,
        renderCell: (params) => (
          <Box height="100%" display="flex" alignItems="center">
            <Chip
              size="small"
              variant="outlined"
              color={params.row.allowAccountCreation ? "primary" : "default"}
              label={
                params.row.allowAccountCreation
                  ? t("identityProviders.status.allowed")
                  : t("identityProviders.status.blocked")
              }
            />
          </Box>
        ),
      },
      {
        field: "scopes",
        headerName: t("identityProviders.columns.scopes"),
        flex: 1,
        minWidth: 160,
        valueGetter: (_, row) => joinList(row.scopes),
        renderCell: (params) => (
          <Tooltip title={joinList(params.row.scopes)}>
            <Typography variant="body2" noWrap>
              {joinList(params.row.scopes)}
            </Typography>
          </Tooltip>
        ),
      },
      {
        field: "allowedEmailDomains",
        headerName: t("identityProviders.columns.domains"),
        flex: 1,
        minWidth: 160,
        valueGetter: (_, row) => joinList(row.allowedEmailDomains),
        renderCell: (params) => (
          <Tooltip title={joinList(params.row.allowedEmailDomains)}>
            <Typography variant="body2" noWrap>
              {joinList(params.row.allowedEmailDomains)}
            </Typography>
          </Tooltip>
        ),
      },
      {
        field: "actions",
        headerName: t("identityProviders.columns.actions"),
        type: "actions",
        width: 96,
        getActions: (params) => [
          <GridActionsCellItem
            key="edit"
            icon={<EditOutlinedIcon />}
            label={t("identityProviders.actions.edit")}
            onClick={() => onEdit(params.row)}
          />,
          <GridActionsCellItem
            key="delete"
            icon={<DeleteOutlineIcon color="error" />}
            label={t("identityProviders.actions.delete")}
            onClick={() => onDelete(params.row)}
          />,
        ],
      },
    ],
    [onDelete, onEdit, t],
  );
};
