import { Alert, Box, Stack, Typography } from "@mui/material";
import { DataGrid } from "@mui/x-data-grid";
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

  const ToolbarSlot = useMemo(() => {
    const ProviderToolbar = () => (
      <OidcProvidersGridToolbar
        createLabel={t("identityProviders.actions.create")}
        loading={providersQuery.isLoading}
        refreshLabel={t("identityProviders.actions.refresh")}
        onCreate={() => setCreateOpen(true)}
        onRefresh={() => void providersQuery.refetch()}
      />
    );

    return ProviderToolbar;
  }, [providersQuery, t]);

  return (
    <Stack spacing={2}>
      <AdminPageSurface>
        <Stack p={3} pb={2} spacing={0.5}>
          <Typography variant="h5" fontWeight={700}>
            {t("identityProviders.title")}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {t("identityProviders.description")}
          </Typography>
        </Stack>

        {(providersQuery.isError || pageError) && (
          <Box px={3} pb={2}>
            <Alert severity="error">
              {pageError ?? t("identityProviders.errors.loadFailed")}
            </Alert>
          </Box>
        )}

        <Box
          sx={{
            height: { xs: 520, md: 640 },
            minHeight: 420,
            maxHeight: "calc(100% - 220px)",
            borderTop: "1px solid",
            borderColor: "divider",
          }}
        >
          <DataGrid
            rows={providers}
            columns={columns}
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
    </Stack>
  );
};
