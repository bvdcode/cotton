import { useState, useCallback, useEffect, useRef } from "react";
import { getFileTypeInfo, isRawImageFile } from "@shared/utils/fileTypes";
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

function supportsInlinePreview(
  fileType: FileType,
  encrypted: boolean,
  rawImage: boolean,
): boolean {
  switch (fileType) {
    case "pdf":
    case "text":
    case "model":
    case "office":
      return true;
    case "image":
    case "video":
    case "audio":
      return encrypted && !rawImage;
    case "document":
    case "archive":
    case "other":
      return false;
  }
}

function exceedsInlinePreviewLimit(
  fileType: FileType,
  encrypted: boolean,
  size: number,
): boolean {
  const inlineLimit = encrypted
    ? CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES
    : previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES;
  return (
    ((fileType === "text" || encrypted) && size > inlineLimit) ||
    (fileType === "office" &&
      size > previewConfig.MAX_OFFICE_PREVIEW_SIZE_BYTES) ||
    (encrypted &&
      fileType === "pdf" &&
      size > previewConfig.MAX_PDF_PREVIEW_SIZE_BYTES)
  );
}

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
      let fileType = typeInfo.type;
      if (
        !supportsInlinePreview(
          fileType,
          encrypted,
          isRawImageFile(fileName, contentType),
        ) ||
        exceedsInlinePreviewLimit(fileType, encrypted, fileSizeBytes ?? 0)
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
