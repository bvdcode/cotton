import { Alert, Box, Stack, Typography, useMediaQuery } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { DataGrid, type GridColumnVisibilityModel } from "@mui/x-data-grid";
import type { AdminUserDto } from "../../../shared/api/adminApi";
import { CreateUserDialog } from "./CreateUserDialog";
import { EditUserDialog } from "./EditUserDialog";
import { AdminPageSurface } from "../components/AdminPageSurface";
import { UsersGridToolbar } from "./components/UsersGridToolbar";
import { useAdminUsersColumns } from "./hooks/useAdminUsersColumns";
import { useAdminUsersData } from "./hooks/useAdminUsersData";

export const AdminUsersPage = () => {
  const { t } = useTranslation(["admin", "common"]);

  const { users, loadState, storageUsageLoading, refresh } = useAdminUsersData();
  const [editingUser, setEditingUser] = useState<AdminUserDto | null>(null);
  const [createOpen, setCreateOpen] = useState(false);

  const isLoading = loadState.kind === "loading";
  const createLabel = t("users.create.button");
  const refreshLabel = t("users.refresh");

  const columns = useAdminUsersColumns({
    storageUsageLoading,
    onEdit: setEditingUser,
  });

  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const columnVisibilityModel: GridColumnVisibilityModel = isMobile
    ? {
        email: false,
        firstName: false,
        lastName: false,
        birthDate: false,
        isTotpEnabled: false,
        activeSessionCount: false,
        storageUsedBytes: false,
        lastActivityAt: false,
      }
    : {};

  const GridToolbarSlot = useMemo(() => {
    const ToolbarSlot = () => (
      <UsersGridToolbar
        createLabel={createLabel}
        loading={isLoading}
        refreshLabel={refreshLabel}
        onCreate={() => setCreateOpen(true)}
        onRefresh={() => void refresh()}
      />
    );

    return ToolbarSlot;
  }, [createLabel, isLoading, refresh, refreshLabel]);

  return (
    <Stack spacing={2}>
      <AdminPageSurface>
        <Stack p={3} pb={2} spacing={0.5}>
          <Typography variant="h5" fontWeight={700}>
            {t("users.title")}
          </Typography>
        </Stack>

        {loadState.kind === "error" && (
          <Box px={3} pb={2}>
            <Alert severity="error">{loadState.message}</Alert>
          </Box>
        )}

        <Box
          sx={{
            height: { xs: 520, md: 640 },
            minHeight: 420,
            maxHeight: "calc(100dvh - 220px)",
            borderTop: "1px solid",
            borderColor: "divider",
          }}
        >
          <DataGrid
            rows={users}
            columns={columns}
            columnVisibilityModel={columnVisibilityModel}
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

      <CreateUserDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
      />
      <EditUserDialog
        open={editingUser !== null}
        user={editingUser}
        onClose={() => setEditingUser(null)}
      />
    </Stack>
  );
};
