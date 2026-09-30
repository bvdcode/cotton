import { Box, Button, Typography } from "@mui/material";
import { Download } from "@mui/icons-material";
import { useTranslation } from "react-i18next";

interface FilePreviewUnavailableProps {
  message?: string;
  onDownload: () => void;
}

export function FilePreviewUnavailable({
  message,
  onDownload,
}: FilePreviewUnavailableProps) {
  const { t } = useTranslation(["share", "common"]);
  return (
    <Box
      display="flex"
      flexDirection="column"
      alignItems="center"
      justifyContent="center"
      flex={1}
      gap={2}
      p={3}
    >
      <Typography align="center">
        {message ?? t("share:unsupported")}
      </Typography>
      <Button variant="contained" startIcon={<Download />} onClick={onDownload}>
        {t("common:actions.download")}
      </Button>
    </Box>
  );
}
