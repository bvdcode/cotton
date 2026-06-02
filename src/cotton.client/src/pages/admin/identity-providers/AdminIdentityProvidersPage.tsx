import { Alert, Box, useMediaQuery } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import { DataGrid, type GridColumnVisibilityModel } from "@mui/x-data-grid";
import { useConfirm } from "material-ui-confirm";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { OidcProviderDto } from "@shared/api/oidcApi";
import {
  useAdminOidcProvidersQuery,
  useDeleteOidcProviderMutation,
} from "@shared/api/queries/oidc";
import { AdminPageSurface } from "../components/AdminPageSurface";
import { OidcProviderFormDialog } from "./OidcProviderFormDialog";
import { OidcProvidersGridToolbar } from "./OidcProvidersGridToolbar";
import { useOidcProviderColumns } from "./useOidcProviderColumns";

export const AdminIdentityProvidersPage = () => {
  const { t } = useTranslation(["admin", "common"]);
  const confirm = useConfirm();
  const providersQuery = useAdminOidcProvidersQuery();
  const deleteMutation = useDeleteOidcProviderMutation();
  const [editingProvider, setEditingProvider] = useState<OidcProviderDto | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [pageError, setPageError] = useState<string | null>(null);

  const providers = providersQuery.data ?? [];

  const handleDelete = async (provider: OidcProviderDto) => {
    const result = await confirm({
      title: t("identityProviders.delete.title", { name: provider.name }),
      description: t("identityProviders.delete.description"),
      confirmationText: t("identityProviders.delete.confirm"),
      cancellationText: t("actions.cancel", { ns: "common" }),
    });

    if (!result.confirmed) return;

    setPageError(null);
    try {
      await deleteMutation.mutateAsync(provider.id);
    } catch {
      setPageError(t("identityProviders.errors.deleteFailed"));
    }
  };

  const columns = useOidcProviderColumns({
    onEdit: setEditingProvider,
    onDelete: (provider) => void handleDelete(provider),
  });

  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const columnVisibilityModel: GridColumnVisibilityModel = isMobile
    ? {
        issuer: false,
        allowAccountCreation: false,
        scopes: false,
        allowedEmailDomains: false,
      }
    : {};

  const ToolbarSlot = useMemo(() => {
    const ProviderToolbar = () => (
      <OidcProvidersGridToolbar
        createLabel={t("identityProviders.actions.create")}
        docsLabel={t("identityProviders.actions.openDocs")}
        loading={providersQuery.isLoading}
        refreshLabel={t("identityProviders.actions.refresh")}
        onCreate={() => setCreateOpen(true)}
        onRefresh={() => void providersQuery.refetch()}
      />
    );

    return ProviderToolbar;
  }, [providersQuery, t]);

  return (
    <Box
      sx={{
        height: "100%",
        minHeight: 0,
        display: "flex",
        flexDirection: "column",
      }}
    >
      <AdminPageSurface fullHeight>
        {(providersQuery.isError || pageError) && (
          <Box p={2} pb={0}>
            <Alert severity="error">
              {pageError ?? t("identityProviders.errors.loadFailed")}
            </Alert>
          </Box>
        )}

        <Box sx={{ flex: 1, minHeight: 0 }}>
          <DataGrid
            rows={providers}
            columns={columns}
            columnVisibilityModel={columnVisibilityModel}
            getRowId={(row) => row.id}
            loading={providersQuery.isLoading}
            disableRowSelectionOnClick
            showToolbar
            label={t("identityProviders.title")}
            pageSizeOptions={[10, 25, 50]}
            initialState={{
              pagination: {
                paginationModel: { pageSize: 25, page: 0 },
              },
            }}
            pagination
            slots={{ toolbar: ToolbarSlot }}
            sx={{
              height: "100%",
              border: 0,
              "& .MuiDataGrid-toolbar": {
                px: 1,
                py: 0.75,
                borderBottom: "1px solid",
                borderColor: "divider",
              },
            }}
          />
        </Box>
      </AdminPageSurface>

      <OidcProviderFormDialog
        open={createOpen}
        provider={null}
        onClose={() => setCreateOpen(false)}
      />
      <OidcProviderFormDialog
        open={editingProvider !== null}
        provider={editingProvider}
        onClose={() => setEditingProvider(null)}
      />
    </Box>
  );
};
