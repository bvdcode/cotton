import * as React from "react";
import { useTranslation } from "react-i18next";
import type { FileType } from "@shared/utils/fileTypes";
import { sharedFoldersApi } from "../../../shared/api/sharedFoldersApi";
import { previewConfig } from "../../../shared/config/previewConfig";

export type SharedTextPreviewState = {
  key: string;
  loading: boolean;
  error: string | null;
  content: string | null;
};

const createIdleTextPreviewState = (key: string): SharedTextPreviewState => ({
  key,
  loading: false,
  error: null,
  content: null,
});

const createLoadingTextPreviewState = (
  key: string,
): SharedTextPreviewState => ({
  key,
  loading: true,
  error: null,
  content: null,
});

const buildTextPreviewKey = (args: {
  open: boolean;
  token: string;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
}): string => {
  if (
    !args.open ||
    args.fileType !== "text" ||
    !args.fileId ||
    !args.fileName
  ) {
    return "";
  }

  return [
    args.token,
    args.fileId,
    args.fileName,
    args.fileSizeBytes ?? "",
  ].join("\u0000");
};

const formatTextPreviewSize = (sizeBytes: number): string => {
  const sizeMB = sizeBytes / 1024 / 1024;
  return sizeMB >= 1
    ? Math.round(sizeMB) + " MB"
    : Math.round(sizeBytes / 1024) + " KB";
};

export const useSharedTextPreview = ({
  fileId,
  fileName,
  fileSizeBytes,
  fileType,
  open,
  token,
}: {
  open: boolean;
  token: string;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
}): SharedTextPreviewState => {
  const { t } = useTranslation(["files"]);
  const textPreviewKey = React.useMemo(
    () =>
      buildTextPreviewKey({
        fileId,
        fileName,
        fileSizeBytes,
        fileType,
        open,
        token,
      }),
    [fileId, fileName, fileSizeBytes, fileType, open, token],
  );
  const sizeError = React.useMemo(() => {
    const sizeBytes = fileSizeBytes;
    if (
      !textPreviewKey ||
      typeof sizeBytes !== "number" ||
      sizeBytes <= previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES
    ) {
      return null;
    }

    return t("preview.errors.fileTooLarge", {
      ns: "files",
      size: formatTextPreviewSize(sizeBytes),
      maxSize:
        Math.round(previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES / 1024) + " KB",
    });
  }, [fileSizeBytes, t, textPreviewKey]);
  const [state, setState] = React.useState<SharedTextPreviewState>(() =>
    createIdleTextPreviewState(textPreviewKey),
  );

  React.useEffect(() => {
    if (!textPreviewKey || sizeError || !fileId) {
      return;
    }

    let cancelled = false;

    void (async () => {
      try {
        const inlineUrl = sharedFoldersApi.buildFileContentUrl(
          token,
          fileId,
          "inline",
        );
        const response = await fetch(inlineUrl);

        if (cancelled) return;

        if (!response.ok) {
          throw new Error(
            t("preview.errors.loadFailed", { ns: "files", error: "" }),
          );
        }

        const content = await response.text();
        if (!cancelled) {
          setState({
            key: textPreviewKey,
            loading: false,
            error: null,
            content,
          });
        }
      } catch (error) {
        if (!cancelled) {
          setState({
            key: textPreviewKey,
            loading: false,
            error:
              error instanceof Error
                ? error.message
                : t("preview.errors.loadFailed", { ns: "files", error: "" }),
            content: null,
          });
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [fileId, token, sizeError, t, textPreviewKey]);

  if (!textPreviewKey) {
    return createIdleTextPreviewState(textPreviewKey);
  }
  if (sizeError) {
    return {
      ...createIdleTextPreviewState(textPreviewKey),
      error: sizeError,
    };
  }

  return state.key === textPreviewKey
    ? state
    : createLoadingTextPreviewState(textPreviewKey);
};
