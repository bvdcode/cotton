import React from "react";
import { PreviewModal, PdfPreview, TextPreview, AudioPreview } from "./preview";
import type { FileType } from "../utils/fileTypes";
import type { AudioPlaylistItem } from "./preview/AudioPreview";
import { Box, IconButton, Paper, Snackbar, Typography } from "@mui/material";
import { Close } from "@mui/icons-material";
import type { SnackbarCloseReason } from "@mui/material";

interface FilePreviewModalProps {
  isOpen: boolean;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
  audioPlaylist?: ReadonlyArray<AudioPlaylistItem> | null;
  onClose: () => void;
  onSaved?: () => void;
}

/**
 * Shared file preview modal component
 * Displays PDF or Text preview based on file type
 */
export const FilePreviewModal: React.FC<FilePreviewModalProps> = ({
  isOpen,
  fileId,
  fileName,
  fileType,
  fileSizeBytes,
  audioPlaylist,
  onClose,
  onSaved,
}) => {
  if (!isOpen || !fileId || !fileName) {
    return null;
  }

  if (fileType === "audio") {
    const handleSnackbarClose = (
      _: Event | React.SyntheticEvent<Element, Event>,
      reason?: SnackbarCloseReason,
    ) => {
      if (reason === "clickaway") return;
      onClose();
    };

    return (
      <Snackbar
        open={isOpen}
        onClose={handleSnackbarClose}
        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
        sx={{
          width: "100%",
          px: { xs: 0, sm: 2 },
          pb: "calc(env(safe-area-inset-bottom, 0px) + 8px)",
        }}
      >
        <Paper
          elevation={8}
          sx={{
            width: "100%",
            maxWidth: 920,
            borderRadius: { xs: 0, sm: 2 },
            bgcolor: "background.paper",
            border: 1,
            borderColor: "divider",
            overflow: "hidden",
          }}
        >
          <Box display="flex" alignItems="center" gap={1} px={2} pt={1}>
            <Typography variant="subtitle2" sx={{ flex: 1, minWidth: 0 }} noWrap>
              {fileName}
            </Typography>
            <IconButton onClick={onClose} size="small">
              <Close fontSize="small" />
            </IconButton>
          </Box>
          <Box px={2} pb={1}>
            <AudioPreview
              nodeFileId={fileId}
              fileName={fileName}
              playlist={audioPlaylist}
            />
          </Box>
        </Paper>
      </Snackbar>
    );
  }

  return (
    <PreviewModal
      open={isOpen}
      onClose={onClose}
      layout={fileType === "pdf" ? "header" : "overlay"}
      title={fileType === "pdf" ? fileName : undefined}
    >
      {fileType === "pdf" && (
        <PdfPreview
          source={{ kind: "fileId", fileId }}
          fileName={fileName}
          fileSizeBytes={fileSizeBytes}
        />
      )}
      {fileType === "text" && (
        <TextPreview
          nodeFileId={fileId}
          fileName={fileName}
          fileSizeBytes={fileSizeBytes}
          onSaved={onSaved}
        />
      )}
    </PreviewModal>
  );
};
