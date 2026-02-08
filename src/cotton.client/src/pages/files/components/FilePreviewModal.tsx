import React from "react";
import { PreviewModal, PdfPreview, TextPreview } from "./preview";
import type { FileType } from "../utils/fileTypes";

interface FilePreviewModalProps {
  isOpen: boolean;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
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
  onClose,
  onSaved,
}) => {
  if (!isOpen || !fileId || !fileName) {
    return null;
  }

  return (
    <PreviewModal
      open={isOpen}
      onClose={onClose}
      layout={fileType === "pdf" ? "header" : "overlay"}
      title={fileType === "pdf" ? fileName : undefined}
    >
      {fileType === "pdf" && (
        <PdfPreview fileId={fileId} fileName={fileName} fileSizeBytes={fileSizeBytes} />
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
