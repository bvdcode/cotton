import { Paper, Stack } from "@mui/material";
import { AdminStorageBackendSettings } from "./AdminStorageBackendSettings";

export const AdminStorageSettingsPage = () => (
  <Stack spacing={2}>
    <Paper>
      <AdminStorageBackendSettings
        sx={{ p: 2, maxWidth: 920, width: "100%" }}
      />
    </Paper>
  </Stack>
);
