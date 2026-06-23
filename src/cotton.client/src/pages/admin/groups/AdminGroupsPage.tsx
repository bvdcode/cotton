import { Alert, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { AdminPageSurface } from "../components/AdminPageSurface";

export const AdminGroupsPage = () => {
  const { t } = useTranslation("admin");

  return (
    <Stack>
      <AdminPageSurface>
        <Stack p={3} spacing={2}>
          <Typography variant="h5" fontWeight={700}>
            {t("groups.title")}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {t("groups.description")}
          </Typography>
          <Alert severity="info">{t("groups.inDevelopment")}</Alert>
        </Stack>
      </AdminPageSurface>
    </Stack>
  );
};
