import * as React from "react";
import { Box, CircularProgress, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { PreviewModal, PdfPreview, ModelPreview } from "../../files/components/preview";
import type { FileType } from "../../files/utils/fileTypes";
import { sharedFoldersApi } from "../../../shared/api/sharedFoldersApi";
import { previewConfig } from "../../../shared/config/previewConfig";
import { ReadOnlyTextViewer } from "./ReadOnlyTextViewer";

interface SharedFilePreviewModalProps {
  open: boolean;
  token: string;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
  contentType: string | null;
  onClose: () => void;
}

export const SharedFilePreviewModal: React.FC<SharedFilePreviewModalProps> = ({
  open,
  token,
  fileId,
  fileName,
  fileType,
  fileSizeBytes,
  contentType,
  onClose,
}) => {
  const { t } = useTranslation(["files", "share", "common"]);

  const [textContent, setTextContent] = React.useState<string | null>(null);
  const [loadingText, setLoadingText] = React.useState<boolean>(false);
  const [textError, setTextError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (!open || fileType !== "text" || !fileId || !fileName) {
      setTextContent(null);
      setTextError(null);
      setLoadingText(false);
      return;
    }

    if (
      typeof fileSizeBytes === "number" &&
      fileSizeBytes > previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES
    ) {
      const sizeMB = fileSizeBytes / 1024 / 1024;
      const maxKB = previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES / 1024;
      setTextError(
        t("preview.errors.fileTooLarge", {
          ns: "files",
          size: sizeMB >= 1 ? `${Math.round(sizeMB)} MB` : `${Math.round(fileSizeBytes / 1024)} KB`,
          maxSize: `${Math.round(maxKB)} KB`,
        }),
      );
      setTextContent(null);
      setLoadingText(false);
      return;
    }

    let cancelled = false;

    setLoadingText(true);
    setTextError(null);
    setTextContent(null);

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
          throw new Error(t("preview.errors.loadFailed", { ns: "files", error: "" }));
        }

        const text = await response.text();
        if (!cancelled) {
          setTextContent(text);
          setLoadingText(false);
        }
      } catch (e) {
        if (!cancelled) {
          setTextError(
            e instanceof Error
              ? e.message
              : t("preview.errors.loadFailed", { ns: "files", error: "" }),
          );
          setLoadingText(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [contentType, fileId, fileName, fileSizeBytes, fileType, open, t, token]);

  if (!open || !fileId || !fileName) {
    return null;
  }

  if (fileType !== "pdf" && fileType !== "text" && fileType !== "model") {
    return null;
  }

  return (
    <PreviewModal
      open={open}
      onClose={onClose}
      layout={fileType === "pdf" ? "header" : "overlay"}
      title={fileType === "pdf" ? fileName : undefined}
    >
      {fileType === "pdf" && (
        <PdfPreview
          source={{
            kind: "url",
            cacheKey: `shared:${token}:${fileId}`,
            getPreviewUrl: async () =>
              sharedFoldersApi.buildFileContentUrl(token, fileId, "inline"),
          }}
          fileName={fileName}
          fileSizeBytes={fileSizeBytes}
        />
      )}

      {fileType === "text" && (
        <Box
          sx={{
            height: "100%",
            minHeight: 0,
            minWidth: 0,
            display: "flex",
            flexDirection: "column",
          }}
        >
          {loadingText && (
            <Box
              sx={{
                flex: 1,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                gap: 1,
              }}
            >
              <CircularProgress size={20} />
              <Typography color="text.secondary">
                {t("loading", { ns: "share" })}
              </Typography>
            </Box>
          )}

          {!loadingText && textError && (
            <Box
              sx={{
                flex: 1,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                px: 2,
              }}
            >
              <Typography color="error">{textError}</Typography>
            </Box>
          )}

          {!loadingText && !textError && textContent !== null && (
            <ReadOnlyTextViewer
              title={fileName}
              fileName={fileName}
              contentType={contentType}
              textContent={textContent}
            />
          )}
        </Box>
      )}

      {fileType === "model" && (
        <ModelPreview
          source={{
            kind: "url",
            url: sharedFoldersApi.buildFileContentUrl(token, fileId, "inline"),
          }}
          fileName={fileName}
          contentType={contentType}
          fileSizeBytes={fileSizeBytes}
        />
      )}
    </PreviewModal>
  );
};
