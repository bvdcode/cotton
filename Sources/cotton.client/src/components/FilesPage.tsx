import { useTranslation } from "react-i18next";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";

const FilesPage = () => {
  const { t } = useTranslation();
  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        {t("files.title", "Files")}
      </Typography>
      <Typography color="text.secondary">
        {t("files.subtitle", "Manage and browse your files here.")}
      </Typography>
    </Box>
  );
};

export default FilesPage;
