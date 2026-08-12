import React from "react";
import {
  Alert,
  Box,
  Dialog,
  DialogContent,
  DialogTitle,
  LinearProgress,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { InterfaceLayoutType } from "@shared/api/layoutsApi";
import Loader from "@shared/ui/Loader";
import type { useTrashRestoreActions } from "../hooks";
import { RestoreConflictDialog } from "./RestoreConflictDialog";

interface TrashPageContentProps {
  children: React.ReactNode;
  hasContent: boolean;
  header: React.ReactNode;
  layoutType: InterfaceLayoutType;
  loadError: string | null;
  loading: boolean;
  restore: ReturnType<typeof useTrashRestoreActions>;
}

export const TrashPageContent: React.FC<TrashPageContentProps> = ({
  children,
  hasContent,
  header,
  layoutType,
  loadError,
  loading,
  restore,
}) => {
  const { t } = useTranslation("trash");

  if (
    loading &&
    !hasContent &&
    !loadError &&
    layoutType !== InterfaceLayoutType.List
  ) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

  const progressPercent =
    restore.progress.total > 0
      ? (restore.progress.current / restore.progress.total) * 100
      : 0;

  return (
    <>
      <Box
        width="100%"
        sx={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          flex: 1,
          ...(layoutType === InterfaceLayoutType.List && {
            minHeight: 0,
            overflow: "hidden",
          }),
        }}
      >
        {header}
        {loadError && (
          <Box mb={1} px={1}>
            <Alert severity="error">{loadError}</Alert>
          </Box>
        )}
        {restore.errors.length > 0 && (
          <Box mb={1} px={1}>
            <Alert severity="warning" onClose={restore.clearErrors}>
              <Box sx={{ whiteSpace: "pre-line" }}>
                {restore.errors.join("\n")}
              </Box>
            </Alert>
          </Box>
        )}
        {children}
      </Box>

      {restore.restoring && (
        <Dialog open disableEscapeKeyDown>
          <DialogTitle>
            {t("restore.inProgress", {
              current: restore.progress.current,
              total: restore.progress.total,
              name: restore.progress.itemName,
            })}
          </DialogTitle>
          <DialogContent>
            <LinearProgress variant="determinate" value={progressPercent} />
          </DialogContent>
        </Dialog>
      )}

      <RestoreConflictDialog
        open={restore.activePrompt !== null}
        itemName={restore.activePrompt?.item.name ?? ""}
        prompt={restore.activePrompt?.prompt ?? null}
        showApplyToAll={restore.progress.total > 1}
        onAnswer={restore.handlePromptAnswer}
      />
    </>
  );
};
