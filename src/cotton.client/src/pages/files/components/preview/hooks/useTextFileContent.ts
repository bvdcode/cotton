import { useState, useEffect } from "react";
import type { Guid } from "../../../../../shared/api/layoutsApi";
import { filesApi } from "../../../../../shared/api/filesApi";
import { useTranslation } from "react-i18next";
import { previewConfig } from "../../../../../shared/config/previewConfig";

export const useTextFileContent = (
  nodeFileId: Guid,
  fileSizeBytes: number | null,
) => {
  const { t } = useTranslation(["files"]);
  const [content, setContent] = useState<string | undefined>(undefined);
  const [originalContent, setOriginalContent] = useState<string>("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const loadContent = async () => {
      try {
        setLoading(true);
        setError(null);

        if (fileSizeBytes && fileSizeBytes > previewConfig.MAX_PREVIEW_SIZE_BYTES) {
          if (!cancelled) {
            const sizeMB = fileSizeBytes / 1024 / 1024;
            const maxMB = previewConfig.MAX_PREVIEW_SIZE_BYTES / 1024;
            setError(
              t("preview.errors.fileTooLarge", {
                ns: "files",
                size:
                  sizeMB >= 1
                    ? `${Math.round(sizeMB)} MB`
                    : `${Math.round(fileSizeBytes / 1024)} KB`,
                maxSize: `${maxMB} KB`,
              }),
            );
            setLoading(false);
          }
          return;
        }

        const downloadUrl = await filesApi.getDownloadLink(nodeFileId);
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
      }
    };

    void loadContent();

    return () => {
      cancelled = true;
    };
  }, [nodeFileId, fileSizeBytes, t]);

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
