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
import type { useTrashRestoreActions } from "../hooks";
import { RestoreConflictDialog } from "./RestoreConflictDialog";

interface TrashPageContentProps {
  children: React.ReactNode;
  header: React.ReactNode;
  layoutType: InterfaceLayoutType;
  loadError: string | null;
  restore: ReturnType<typeof useTrashRestoreActions>;
}

export const TrashPageContent: React.FC<TrashPageContentProps> = ({
  children,
  header,
  layoutType,
  loadError,
  restore,
}) => {
  const { t } = useTranslation("trash");

  const progressPercent =
    restore.progress.total > 0
      ? (restore.progress.current / restore.progress.total) * 100
      : 0;

  return (
    <>
      <Box
        sx={{
          width: "100%",
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
          <Box
            sx={{
              mb: 1,
              px: 1,
            }}
          >
            <Alert severity="error">{loadError}</Alert>
          </Box>
        )}
        {restore.errors.length > 0 && (
          <Box
            sx={{
              mb: 1,
              px: 1,
            }}
          >
            <Alert severity="warning" onClose={restore.clearErrors}>
              <Box sx={{ whiteSpace: "pre-line" }}>
                {restore.errors.join("\n")}
              </Box>
            </Alert>
          </Box>
        )}
        {children}
      </Box>

      {restore.restoring && restore.activePrompt === null && (
        <Dialog open maxWidth="xs" fullWidth>
          <DialogTitle>
            {t("restore.inProgress", {
              total: restore.progress.total,
            })}
          </DialogTitle>
          <DialogContent>
            <LinearProgress
              variant={
                restore.requestInFlight ? "indeterminate" : "determinate"
              }
              value={progressPercent}
            />
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
