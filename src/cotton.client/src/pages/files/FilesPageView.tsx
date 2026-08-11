import React from "react";
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  List,
  ListItem,
  ListItemText,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { FilePreviewModal, MediaLightbox } from "@shared/ui/preview";
import {
  DraggingOverlay,
  FileConflictDialog,
  FileListViewFactory,
  FolderEncryptionActionPrompt,
  PageHeader,
} from "./components";
import { FileVersionsDialog } from "./components/FileVersionsDialog";
import { useFileUpload } from "./hooks/useFileUpload";
import { type FileListPageLogic } from "./hooks/useFileListPageLogic";
import {
  getDropPreparationCaption,
  getDropPreparationTitle,
} from "./utils/dropPreparation";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { readEnvelopeFromPreferences } from "../../shared/crypto";
import Loader from "../../shared/ui/Loader";
import { blurredDialogBackdropSlotProps } from "../../shared/ui/dialogBackdrop";
import { ClientEncryptionUnlockForm } from "../profile/components/ClientEncryptionUnlockForm";
import type {
  ClientEncryptionUnlockPrompt,
  FolderEncryptionPromptModel,
} from "./filesPageModel";

type FilesPageViewProps = {
  activeUnlockPrompt: ClientEncryptionUnlockPrompt | null;
  clientEncryptionEnvelope: ReturnType<typeof readEnvelopeFromPreferences>;
  closePreview: FileListPageLogic["interaction"]["closePreview"];
  error: string | null;
  fileListViewProps: React.ComponentProps<typeof FileListViewFactory>;
  fileUpload: ReturnType<typeof useFileUpload>;
  folderEncryptionPrompt: FolderEncryptionPromptModel | null;
  getDownloadUrl: FileListPageLogic["interaction"]["getDownloadUrl"];
  getSignedMediaUrl: FileListPageLogic["interaction"]["getSignedMediaUrl"];
  handleCloseVersions: () => void;
  handleLightboxDelete: (
    item: FileListPageLogic["interaction"]["mediaItems"][number],
  ) => Promise<void>;
  handleUnlockCancel: () => void;
  handleUnlockSuccess: () => void;
  handleVersionsChanged: () => void;
  layoutType: InterfaceLayoutType;
  lightboxIndex: number;
  lightboxOpen: boolean;
  mediaItems: FileListPageLogic["interaction"]["mediaItems"];
  pageHeaderProps: React.ComponentProps<typeof PageHeader>;
  previewState: FileListPageLogic["interaction"]["previewState"];
  refreshCurrentNodeContent: () => void;
  setLightboxOpen: FileListPageLogic["interaction"]["setLightboxOpen"];
  shouldRenderFileList: boolean;
  smoothGalleryTransitions: boolean;
  t: ReturnType<typeof useTranslation>["t"];
  unlockDialogOpen: boolean;
  versionDialogFile: { id: string; name: string } | null;
};

export const FilesPageView: React.FC<FilesPageViewProps> = ({
  activeUnlockPrompt,
  clientEncryptionEnvelope,
  closePreview,
  error,
  fileListViewProps,
  fileUpload,
  folderEncryptionPrompt,
  getDownloadUrl,
  getSignedMediaUrl,
  handleCloseVersions,
  handleLightboxDelete,
  handleUnlockCancel,
  handleUnlockSuccess,
  handleVersionsChanged,
  layoutType,
  lightboxIndex,
  lightboxOpen,
  mediaItems,
  pageHeaderProps,
  previewState,
  refreshCurrentNodeContent,
  setLightboxOpen,
  shouldRenderFileList,
  smoothGalleryTransitions,
  t,
  unlockDialogOpen,
  versionDialogFile,
}) => (
  <>
    <FilesDropPreparationLoader fileUpload={fileUpload} t={t} />
    <FilesPageContentPanel
      error={error}
      fileListViewProps={fileListViewProps}
      fileUpload={fileUpload}
      layoutType={layoutType}
      pageHeaderProps={pageHeaderProps}
      shouldRenderFileList={shouldRenderFileList}
      t={t}
      unlockDialogOpen={unlockDialogOpen}
    />
    <FilesPreviewLayers
      closePreview={closePreview}
      fileUpload={fileUpload}
      getDownloadUrl={getDownloadUrl}
      getSignedMediaUrl={getSignedMediaUrl}
      handleCloseVersions={handleCloseVersions}
      handleLightboxDelete={handleLightboxDelete}
      handleVersionsChanged={handleVersionsChanged}
      lightboxIndex={lightboxIndex}
      lightboxOpen={lightboxOpen}
      mediaItems={mediaItems}
      previewState={previewState}
      refreshCurrentNodeContent={refreshCurrentNodeContent}
      setLightboxOpen={setLightboxOpen}
      smoothGalleryTransitions={smoothGalleryTransitions}
      versionDialogFile={versionDialogFile}
    />
    <FilesEncryptionPrompts
      activeUnlockPrompt={activeUnlockPrompt}
      clientEncryptionEnvelope={clientEncryptionEnvelope}
      folderEncryptionPrompt={folderEncryptionPrompt}
      handleUnlockCancel={handleUnlockCancel}
      handleUnlockSuccess={handleUnlockSuccess}
      t={t}
      unlockDialogOpen={unlockDialogOpen}
    />
  </>
);

type FilesPageContentPanelProps = Pick<
  FilesPageViewProps,
  | "error"
  | "fileListViewProps"
  | "fileUpload"
  | "layoutType"
  | "pageHeaderProps"
  | "shouldRenderFileList"
  | "t"
  | "unlockDialogOpen"
>;

const FilesDropPreparationLoader: React.FC<
  Pick<FilesPageViewProps, "fileUpload" | "t">
> = ({ fileUpload, t }) =>
  fileUpload.dropPreparation.active ? (
    <Loader
      overlay
      title={getDropPreparationTitle(t, fileUpload.dropPreparation)}
      caption={getDropPreparationCaption(t, fileUpload.dropPreparation)}
    />
  ) : null;

const FilesPageContentPanel: React.FC<FilesPageContentPanelProps> = ({
  error,
  fileListViewProps,
  fileUpload,
  layoutType,
  pageHeaderProps,
  shouldRenderFileList,
  t,
  unlockDialogOpen,
}) => (
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
      <PageHeader {...pageHeaderProps} />
      {error && (
        <Box mb={1} px={1}>
          <Alert severity="error">{error}</Alert>
        </Box>
      )}
      {shouldRenderFileList && (
        <Box
          sx={
            layoutType === InterfaceLayoutType.List
              ? { flex: 1, minHeight: 0, overflow: "hidden", pb: 1 }
              : {}
          }
        >
          <FileListViewFactory {...fileListViewProps} />
        </Box>
      )}
    </Box>
  </>
);

type FilesPreviewLayersProps = Pick<
  FilesPageViewProps,
  | "closePreview"
  | "fileUpload"
  | "getDownloadUrl"
  | "getSignedMediaUrl"
  | "handleCloseVersions"
  | "handleLightboxDelete"
  | "handleVersionsChanged"
  | "lightboxIndex"
  | "lightboxOpen"
  | "mediaItems"
  | "previewState"
  | "refreshCurrentNodeContent"
  | "setLightboxOpen"
  | "smoothGalleryTransitions"
  | "versionDialogFile"
>;

const FilesPreviewLayers: React.FC<FilesPreviewLayersProps> = ({
  closePreview,
  fileUpload,
  getDownloadUrl,
  getSignedMediaUrl,
  handleCloseVersions,
  handleLightboxDelete,
  handleVersionsChanged,
  lightboxIndex,
  lightboxOpen,
  mediaItems,
  previewState,
  refreshCurrentNodeContent,
  setLightboxOpen,
  smoothGalleryTransitions,
  versionDialogFile,
}) => (
  <>
    <FilePreviewModal
      isOpen={previewState.isOpen}
      fileId={previewState.fileId}
      fileName={previewState.fileName}
      fileType={previewState.fileType}
      fileSizeBytes={previewState.fileSizeBytes}
      file={previewState.file}
      onClose={closePreview}
      onSaved={refreshCurrentNodeContent}
    />

    {lightboxOpen && mediaItems.length > 0 && (
      <MediaLightbox
        items={mediaItems}
        open={lightboxOpen}
        initialIndex={lightboxIndex}
        onClose={() => setLightboxOpen(false)}
        getSignedMediaUrl={getSignedMediaUrl}
        getDownloadUrl={getDownloadUrl}
        onDelete={handleLightboxDelete}
        smoothTransitions={smoothGalleryTransitions}
      />
    )}

    <FileConflictDialog
      open={fileUpload.conflictDialog.state.open}
      newName={fileUpload.conflictDialog.state.newName}
      canOverwrite={fileUpload.conflictDialog.state.canOverwrite}
      onResolve={fileUpload.conflictDialog.onResolve}
      onExited={fileUpload.conflictDialog.onExited}
    />

    <SkippedUploadItemsDialog
      open={fileUpload.skippedItemsDialog.state.open}
      total={fileUpload.skippedItemsDialog.state.total}
      items={fileUpload.skippedItemsDialog.state.items}
      truncated={fileUpload.skippedItemsDialog.state.truncated}
      onClose={fileUpload.skippedItemsDialog.onClose}
    />

    <FileVersionsDialog
      open={versionDialogFile !== null}
      fileId={versionDialogFile?.id ?? null}
      fileName={versionDialogFile?.name ?? ""}
      onClose={handleCloseVersions}
      onRestored={handleVersionsChanged}
    />
  </>
);

type SkippedUploadItemsDialogProps = {
  open: boolean;
  total: number;
  items: string[];
  truncated: boolean;
  onClose: () => void;
};

const SkippedUploadItemsDialog: React.FC<SkippedUploadItemsDialogProps> = ({
  open,
  total,
  items,
  truncated,
  onClose,
}) => {
  const { t } = useTranslation(["files", "common"]);

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="md">
      <DialogTitle>
        {t("uploadDrop.skippedDialog.title", { ns: "files" })}
      </DialogTitle>
      <DialogContent dividers>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          {t("uploadDrop.skippedDialog.description", {
            ns: "files",
            count: total,
          })}
        </Typography>

        {items.length > 0 && (
          <List dense disablePadding sx={{ maxHeight: 360, overflow: "auto" }}>
            {items.map((item, index) => (
              <ListItem key={`${item}-${index}`} disableGutters>
                <ListItemText
                  primary={item}
                  primaryTypographyProps={{
                    variant: "body2",
                    sx: { overflowWrap: "anywhere", wordBreak: "break-word" },
                  }}
                />
              </ListItem>
            ))}
          </List>
        )}

        {truncated && (
          <Alert severity="info" sx={{ mt: 2 }}>
            {t("uploadDrop.skippedDialog.truncated", {
              ns: "files",
              count: items.length,
            })}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{t("common:actions.close")}</Button>
      </DialogActions>
    </Dialog>
  );
};

type FilesEncryptionPromptsProps = Pick<
  FilesPageViewProps,
  | "activeUnlockPrompt"
  | "clientEncryptionEnvelope"
  | "folderEncryptionPrompt"
  | "handleUnlockCancel"
  | "handleUnlockSuccess"
  | "t"
  | "unlockDialogOpen"
>;

const FilesEncryptionPrompts: React.FC<FilesEncryptionPromptsProps> = ({
  activeUnlockPrompt,
  clientEncryptionEnvelope,
  folderEncryptionPrompt,
  handleUnlockCancel,
  handleUnlockSuccess,
  t,
  unlockDialogOpen,
}) => (
  <>
    {folderEncryptionPrompt && (
      <FolderEncryptionActionPrompt
        action={folderEncryptionPrompt.action}
        disabled={folderEncryptionPrompt.disabled}
        message={folderEncryptionPrompt.message}
        onAction={folderEncryptionPrompt.onAction}
        severity={folderEncryptionPrompt.severity}
      />
    )}

    <Dialog
      open={unlockDialogOpen}
      onClose={handleUnlockCancel}
      fullWidth
      maxWidth="sm"
      slotProps={blurredDialogBackdropSlotProps}
    >
      <DialogTitle>
        {activeUnlockPrompt?.kind === "current"
          ? t("clientEncryption.unlockDialog.currentTitle", { ns: "files" })
          : t("clientEncryption.unlockDialog.title", { ns: "files" })}
      </DialogTitle>
      {clientEncryptionEnvelope && (
        <ClientEncryptionUnlockForm
          envelope={clientEncryptionEnvelope}
          onCancel={handleUnlockCancel}
          onSuccess={handleUnlockSuccess}
          cancelLabel={
            activeUnlockPrompt?.kind === "current"
              ? t("clientEncryption.unlockDialog.goHome", { ns: "files" })
              : undefined
          }
        />
      )}
    </Dialog>
  </>
);
