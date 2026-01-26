import React from "react";
import { Box, CircularProgress, Alert, Button } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { Guid } from "../../../../../shared/api/layoutsApi";
import { filesApi } from "../../../../../shared/api/filesApi";

interface TextPreviewLoadingProps {
  loading: boolean;
}

export const TextPreviewLoading: React.FC<TextPreviewLoadingProps> = ({
  loading,
}) => {
  if (!loading) return null;

  return (
    <Box
      display="flex"
      justifyContent="center"
      alignItems="center"
      minHeight={400}
    >
      <CircularProgress />
    </Box>
  );
};

interface TextPreviewErrorProps {
  error: string | null;
  isFileTooLarge: boolean;
  nodeFileId: Guid;
  fileName: string;
}

export const TextPreviewError: React.FC<TextPreviewErrorProps> = ({
  error,
  isFileTooLarge,
  nodeFileId,
  fileName,
}) => {
  const { t } = useTranslation(["common"]);

  if (!error) return null;

  return (
    <Box p={3}>
      <Alert
        severity="error"
        action={
          isFileTooLarge ? (
            <Button
              color="inherit"
              size="small"
              onClick={() => {
                void filesApi.getDownloadLink(nodeFileId).then((url: string) => {
                  const link = document.createElement("a");
                  link.href = url;
                  link.download = fileName;
                  link.click();
                });
              }}
            >
              {t("common:actions.download")}
            </Button>
          ) : undefined
        }
      >
        {error}
      </Alert>
    </Box>
  );
};
