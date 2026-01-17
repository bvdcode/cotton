import { useState, useCallback } from "react";
import { getFileTypeInfo } from "../utils/fileTypes";
import type { FileType } from "../utils/fileTypes";

interface PreviewState {
  isOpen: boolean;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
}

export const useFilePreview = () => {
  const [previewState, setPreviewState] = useState<PreviewState>({
    isOpen: false,
    fileId: null,
    fileName: null,
    fileType: null,
  });

  const openPreview = useCallback((fileId: string, fileName: string) => {
    const typeInfo = getFileTypeInfo(fileName);
    if (typeInfo.supportsInlineView) {
      setPreviewState({
        isOpen: true,
        fileId,
        fileName,
        fileType: typeInfo.type,
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
    });
  }, []);

  return {
    previewState,
    openPreview,
    closePreview,
  };
};
