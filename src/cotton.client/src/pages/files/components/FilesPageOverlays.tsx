import React, { useCallback } from "react";
import { Dialog, DialogTitle } from "@mui/material";
import { useTranslation } from "react-i18next";
import { FilePreviewModal, MediaLightbox } from "@shared/ui/preview";
import { refreshNodeContent } from "@shared/store/nodesActions";
import Loader from "@shared/ui/Loader";
import { blurredDialogBackdropSlotProps } from "@shared/ui/dialogBackdrop";
import { ClientEncryptionUnlockForm } from "../../profile/components/ClientEncryptionUnlockForm";
import type { useFilesEncryptionController } from "../hooks/useFilesEncryptionController";
import type { FileListPageLogic } from "../hooks/useFileListPageLogic";
import type { useFileUpload } from "../hooks/useFileUpload";
import {
  getDropPreparationCaption,
  getDropPreparationTitle,
} from "../utils/dropPreparation";
import { FileConflictDialog } from "./FileConflictDialog";
import { FolderEncryptionActionPrompt } from "./FolderEncryptionActionPrompt";
import { SkippedUploadItemsDialog } from "./SkippedUploadItemsDialog";

interface FilesDropPreparationLoaderProps {
  fileUpload: ReturnType<typeof useFileUpload>;
}

export const FilesDropPreparationLoader: React.FC<
  FilesDropPreparationLoaderProps
> = ({ fileUpload }) => {
  const { t } = useTranslation("files");

  return fileUpload.dropPreparation.active ? (
    <Loader
      overlay
      title={getDropPreparationTitle(t, fileUpload.dropPreparation)}
      caption={getDropPreparationCaption(t, fileUpload.dropPreparation)}
    />
  ) : null;
};

interface FilesPageOverlaysProps {
  encryption: ReturnType<typeof useFilesEncryptionController>;
  fileUpload: ReturnType<typeof useFileUpload>;
  handleLightboxDelete: (
    item: FileListPageLogic["interaction"]["mediaItems"][number],
  ) => Promise<void>;
  interaction: FileListPageLogic["interaction"];
  nodeId: string | null;
  smoothGalleryTransitions: boolean;
}

export const FilesPageOverlays: React.FC<FilesPageOverlaysProps> = ({
  encryption,
  fileUpload,
  handleLightboxDelete,
  interaction,
  nodeId,
  smoothGalleryTransitions,
}) => {
  const { t } = useTranslation("files");
  const refreshCurrentNodeContent = useCallback(() => {
    if (nodeId) {
      void refreshNodeContent(nodeId);
    }
  }, [nodeId]);

  return (
    <>
      <FilePreviewModal
        isOpen={interaction.previewState.isOpen}
        fileId={interaction.previewState.fileId}
        fileName={interaction.previewState.fileName}
        fileType={interaction.previewState.fileType}
        fileSizeBytes={interaction.previewState.fileSizeBytes}
        file={interaction.previewState.file}
        onClose={interaction.closePreview}
        onSaved={refreshCurrentNodeContent}
      />

      {interaction.lightboxOpen && interaction.mediaItems.length > 0 && (
        <MediaLightbox
          items={interaction.mediaItems}
          open={interaction.lightboxOpen}
          initialIndex={interaction.lightboxIndex}
          onClose={() => interaction.setLightboxOpen(false)}
          getSignedMediaUrl={interaction.getSignedMediaUrl}
          getDownloadUrl={interaction.getDownloadUrl}
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

      {encryption.folderEncryptionPrompt && (
        <FolderEncryptionActionPrompt
          action={encryption.folderEncryptionPrompt.action}
          disabled={encryption.folderEncryptionPrompt.disabled}
          message={encryption.folderEncryptionPrompt.message}
          onAction={encryption.folderEncryptionPrompt.onAction}
          severity={encryption.folderEncryptionPrompt.severity}
        />
      )}

      <Dialog
        open={encryption.unlockDialogOpen}
        onClose={encryption.handleUnlockCancel}
        fullWidth
        maxWidth="sm"
        slotProps={blurredDialogBackdropSlotProps}
      >
        <DialogTitle>
          {encryption.activeUnlockPrompt?.kind === "current"
            ? t("clientEncryption.unlockDialog.currentTitle")
            : t("clientEncryption.unlockDialog.title")}
        </DialogTitle>
        {encryption.clientEncryptionEnvelope && (
          <ClientEncryptionUnlockForm
            envelope={encryption.clientEncryptionEnvelope}
            onCancel={encryption.handleUnlockCancel}
            onSuccess={encryption.handleUnlockSuccess}
            cancelLabel={
              encryption.activeUnlockPrompt?.kind === "current"
                ? t("clientEncryption.unlockDialog.goHome")
                : undefined
            }
          />
        )}
      </Dialog>
    </>
  );
};
