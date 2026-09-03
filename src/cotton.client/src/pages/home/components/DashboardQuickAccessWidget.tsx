import { DeleteOutline, FolderOpen, Search } from "@mui/icons-material";
import { Button, Stack } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

export const DashboardQuickAccessWidget = () => {
  const { t } = useTranslation("home");
  const navigate = useNavigate();

  return (
    <Stack gap={1}>
      <Button
        variant="contained"
        startIcon={<FolderOpen />}
        onClick={() => navigate("/files")}
      >
        {t("dashboard.quickAccess.files")}
      </Button>
      <Button
        variant="outlined"
        startIcon={<Search />}
        onClick={() => navigate("/search")}
      >
        {t("dashboard.quickAccess.search")}
      </Button>
      <Button
        variant="outlined"
        startIcon={<DeleteOutline />}
        onClick={() => navigate("/trash")}
      >
        {t("dashboard.quickAccess.trash")}
      </Button>
    </Stack>
  );
};
