import React from "react";
import { Alert, Box } from "@mui/material";
import { useTranslation } from "react-i18next";
import { InterfaceLayoutType } from "@shared/api/layoutsApi";
import { DraggingOverlay } from "./DraggingOverlay";
import type { useFileUpload } from "../hooks/useFileUpload";

interface FilesPageContentProps {
  children: React.ReactNode;
  error: string | null;
  fileUpload: ReturnType<typeof useFileUpload>;
  header: React.ReactNode;
  layoutType: InterfaceLayoutType;
  unlockDialogOpen: boolean;
}

export const FilesPageContent: React.FC<FilesPageContentProps> = ({
  children,
  error,
  fileUpload,
  header,
  layoutType,
  unlockDialogOpen,
}) => {
  const { t } = useTranslation("files");

  return (
    <>
      <DraggingOverlay
        open={fileUpload.isDragging}
        onDragEnter={fileUpload.handleDragEnter}
        onDragOver={fileUpload.handleDragOver}
        onDragLeave={fileUpload.handleDragLeave}
        onDrop={fileUpload.handleDrop}
        label={t("actions.dropFiles")}
      />
      <Box
        width="100%"
        onDragEnter={fileUpload.handleDragEnter}
        onDragOver={fileUpload.handleDragOver}
        onDragLeave={fileUpload.handleDragLeave}
        onDrop={fileUpload.handleDrop}
        sx={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          flex: 1,
          ...(layoutType === InterfaceLayoutType.List && {
            minHeight: 0,
            overflow: "hidden",
          }),
          ...(unlockDialogOpen && {
            filter: "blur(4px)",
            pointerEvents: "none",
            transition: "filter 160ms ease",
            userSelect: "none",
          }),
        }}
      >
        {header}
        {error && (
          <Box mb={1} px={1}>
            <Alert severity="error">{error}</Alert>
          </Box>
        )}
        {children}
      </Box>
    </>
  );
};
