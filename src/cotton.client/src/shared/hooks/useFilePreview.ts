import { useState, useCallback, useEffect, useRef } from "react";
import { getFileTypeInfo } from "@shared/utils/fileTypes";
import type { FileType } from "@shared/utils/fileTypes";
import { previewConfig } from "../config/previewConfig";
import type { NodeFileManifestDto } from "../api/nodesApi";
import {
  CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES,
  isFileEncrypted,
} from "../crypto";
import { readStringProperty } from "../utils/typeGuards";

interface PreviewState {
  isOpen: boolean;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
  file: NodeFileManifestDto | null;
}

const PREVIEW_HISTORY_STATE = "preview";

export const useFilePreview = () => {
  const [previewState, setPreviewState] = useState<PreviewState>({
    isOpen: false,
    fileId: null,
    fileName: null,
    fileType: null,
    fileSizeBytes: null,
    file: null,
  });

  const historyPushedRef = useRef(false);

  const closePreviewInternal = useCallback(() => {
    setPreviewState({
      isOpen: false,
      fileId: null,
      fileName: null,
      fileType: null,
      fileSizeBytes: null,
      file: null,
    });
  }, []);

  const openPreview = useCallback(
    (
      fileId: string,
      fileName: string,
      fileSizeBytes?: number,
      contentType?: string | null,
      file?: NodeFileManifestDto | null,
    ) => {
      const typeInfo = getFileTypeInfo(fileName, contentType);
      const encrypted = file ? isFileEncrypted(file.metadata) : false;
      const textPreviewLimit = encrypted
        ? CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES
        : previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES;
      let fileType = typeInfo.type;
      if (
        (fileType !== "pdf" && fileType !== "text" && fileType !== "model") ||
        (encrypted && fileType !== "text") ||
        (fileType === "text" && (fileSizeBytes ?? 0) > textPreviewLimit)
      ) {
        fileType = "other";
      }

      setPreviewState({
        isOpen: true,
        fileId,
        fileName,
        fileType,
        fileSizeBytes: fileSizeBytes ?? null,
        file: file ?? null,
      });
      window.history.pushState({ overlay: PREVIEW_HISTORY_STATE }, "");
      historyPushedRef.current = true;
    },
    [],
  );

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
        !(readStringProperty(e.state, "overlay") === PREVIEW_HISTORY_STATE)
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
