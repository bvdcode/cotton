import { DeleteOutline, FolderOpen, Search } from "@mui/icons-material";
import { Box, Button } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

export const DashboardQuickAccessWidget = () => {
  const { t } = useTranslation("home");
  const navigate = useNavigate();

  const buttonSx = {
    aspectRatio: "1 / 1",
    flexDirection: "column",
    gap: 1,
    minWidth: 0,
    p: 1,
    "& .MuiButton-startIcon": {
      m: 0,
      "& > svg": { fontSize: 28 },
    },
  } as const;

  return (
    <Box
      display="grid"
      gridTemplateColumns="repeat(3, minmax(0, 1fr))"
      gap={1}
      width="100%"
    >
      <Button
        variant="contained"
        startIcon={<FolderOpen />}
        onClick={() => navigate("/files")}
        sx={buttonSx}
      >
        {t("dashboard.quickAccess.files")}
      </Button>
      <Button
        variant="outlined"
        startIcon={<Search />}
        onClick={() => navigate("/search")}
        sx={buttonSx}
      >
        {t("dashboard.quickAccess.search")}
      </Button>
      <Button
        variant="outlined"
        startIcon={<DeleteOutline />}
        onClick={() => navigate("/trash")}
        sx={buttonSx}
      >
        {t("dashboard.quickAccess.trash")}
      </Button>
    </Box>
  );
};
