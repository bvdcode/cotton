import { Alert, Stack } from "@mui/material";
import { useTranslation } from "react-i18next";
import { AdminPageSurface } from "../components/AdminPageSurface";
import { AdminPageHeader } from "../components/AdminPageHeader";

export const AdminGroupsPage = () => {
  const { t } = useTranslation("admin");

  return (
    <Stack>
      <AdminPageSurface>
        <Stack p={3} spacing={2}>
          <AdminPageHeader
            title={t("groups.title")}
            description={t("groups.description")}
          />
          <Alert severity="info">{t("groups.inDevelopment")}</Alert>
        </Stack>
      </AdminPageSurface>
    </Stack>
  );
};
