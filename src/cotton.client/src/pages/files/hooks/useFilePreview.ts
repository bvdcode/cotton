import { useState, useCallback, useEffect, useRef } from "react";
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

const PREVIEW_HISTORY_STATE = "preview";

export const useFilePreview = () => {
  const [previewState, setPreviewState] = useState<PreviewState>({
    isOpen: false,
    fileId: null,
    fileName: null,
    fileType: null,
    fileSizeBytes: null,
  });

  const historyPushedRef = useRef(false);

  const closePreviewInternal = useCallback(() => {
    setPreviewState({
      isOpen: false,
      fileId: null,
      fileName: null,
      fileType: null,
      fileSizeBytes: null,
    });
  }, []);

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
      window.history.pushState({ overlay: PREVIEW_HISTORY_STATE }, "");
      historyPushedRef.current = true;
      return true;
    }
    return false;
  }, []);

  const closePreview = useCallback(() => {
    closePreviewInternal();
    if (historyPushedRef.current) {
      historyPushedRef.current = false;
      window.history.back();
    }
  }, [closePreviewInternal]);

  useEffect(() => {
    const handlePopState = (e: PopStateEvent) => {
      if (
        historyPushedRef.current &&
        !(e.state && (e.state as { overlay?: string }).overlay === PREVIEW_HISTORY_STATE)
      ) {
        historyPushedRef.current = false;
        closePreviewInternal();
      }
    };

    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, [closePreviewInternal]);

  return {
    previewState,
    openPreview,
    closePreview,
  };
};
