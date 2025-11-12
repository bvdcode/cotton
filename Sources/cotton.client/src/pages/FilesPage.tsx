import { Box } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { FunctionComponent } from "react";

const FilesPage: FunctionComponent = () => {
  const { t } = useTranslation();

  return (
    <Box>
      <Box display="flex" width="100%" height="100%">
        {t("filesPage.title")}
      </Box>
    </Box>
  );
};

export default FilesPage;
