import { useState, useCallback } from "react";
import { getFileTypeInfo } from "../utils/fileTypes";
import type { FileType } from "../utils/fileTypes";
import { previewConfig } from "../../../shared/config/previewConfig";

interface PreviewState {
  isOpen: boolean;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
}

export const useFilePreview = () => {
  const [previewState, setPreviewState] = useState<PreviewState>({
    isOpen: false,
    fileId: null,
    fileName: null,
    fileType: null,
    fileSizeBytes: null,
  });

  const openPreview = useCallback((fileId: string, fileName: string, fileSizeBytes?: number) => {
    const typeInfo = getFileTypeInfo(fileName);
    if (typeInfo.supportsInlineView) {
      if (typeInfo.type === 'text' && fileSizeBytes && fileSizeBytes > previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES) {
        return false;
      }
      
      setPreviewState({
        isOpen: true,
        fileId,
        fileName,
        fileType: typeInfo.type,
        fileSizeBytes: fileSizeBytes ?? null,
      });
      return true;
    }
    return false;
  }, []);

  const closePreview = useCallback(() => {
    setPreviewState({
      isOpen: false,
      fileId: null,
      fileName: null,
      fileType: null,
      fileSizeBytes: null,
    });
  }, []);

  return {
    previewState,
    openPreview,
    closePreview,
  };
};
