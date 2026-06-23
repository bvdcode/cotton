import { useState, useEffect } from "react";
import type { Guid } from "../../../api/layoutsApi";
import type { NodeFileManifestDto } from "../../../api/nodesApi";
import { filesApi } from "../../../api/filesApi";
import { useTranslation } from "react-i18next";
import { previewConfig } from "../../../config/previewConfig";
import {
  CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES,
  getReadableFileUrl,
  isFileEncrypted,
  type ReadableFileHandle,
} from "../../../crypto";
import { formatBytes } from "../../../utils/formatBytes";

export const useTextFileContent = (
  nodeFileId: Guid,
  fileSizeBytes: number | null,
  sourceFile?: NodeFileManifestDto | null,
) => {
  const { t } = useTranslation(["files"]);
  const [content, setContent] = useState<string | undefined>(undefined);
  const [originalContent, setOriginalContent] = useState<string>("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const loadContent = async () => {
      let readableFile: ReadableFileHandle | null = null;

      try {
        setLoading(true);
        setError(null);

        const encrypted = sourceFile
          ? isFileEncrypted(sourceFile.metadata)
          : false;
        const maxPreviewSizeBytes = encrypted
          ? CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES
          : previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES;

        if (fileSizeBytes && fileSizeBytes > maxPreviewSizeBytes) {
          if (!cancelled) {
            setError(
              t("preview.errors.fileTooLarge", {
                ns: "files",
                size: formatBytes(fileSizeBytes),
                maxSize: formatBytes(maxPreviewSizeBytes),
              }),
            );
            setLoading(false);
          }
          return;
        }

        const downloadUrl =
          encrypted && sourceFile
            ? (readableFile = await getReadableFileUrl(sourceFile)).url
            : await filesApi.getDownloadLink(nodeFileId);
        const response = await fetch(downloadUrl);

        if (!response.ok) {
          const errorSuffix = response.statusText
            ? `: ${response.statusText}`
            : "";
          throw new Error(
            t("preview.errors.loadFailed", { ns: "files", error: errorSuffix }),
          );
        }

        const text = await response.text();
        if (!cancelled) {
          setContent(text);
          setOriginalContent(text);
          setLoading(false);
        }
      } catch (err) {
        if (!cancelled) {
          setError(
            err instanceof Error
              ? err.message
              : t("preview.errors.loadFailed", { ns: "files", error: "" }),
          );
          setLoading(false);
        }
      } finally {
        readableFile?.revoke();
      }
    };

    void loadContent();

    return () => {
      cancelled = true;
    };
  }, [nodeFileId, fileSizeBytes, sourceFile, t]);

  return {
    content,
    setContent,
    originalContent,
    setOriginalContent,
    loading,
    error,
    setError,
    isFileTooLarge: false,
  };
};
