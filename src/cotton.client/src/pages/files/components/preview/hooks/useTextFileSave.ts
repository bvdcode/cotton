import { useState } from "react";
import type { Guid } from "../../../../../shared/api/layoutsApi";
import { filesApi } from "../../../../../shared/api/filesApi";
import { uploadBlobToChunks } from "../../../../../shared/upload";
import { useServerSettings } from "../../../../../shared/store/useServerSettings";
import { useTranslation } from "react-i18next";

export const useTextFileSave = (
  nodeFileId: Guid,
  fileName: string,
  originalContent: string,
  setOriginalContent: (content: string) => void,
  onSaved?: () => void,
) => {
  const { t } = useTranslation(["files"]);
  const { data: serverSettings } = useServerSettings();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSave = async (content: string) => {
    if (!content || content === originalContent || !serverSettings) return;

    try {
      setSaving(true);
      setError(null);

      const blob = new Blob([content], { type: "text/plain" });
      const { chunkHashes, fileHash } = await uploadBlobToChunks({
        blob,
        fileName,
        server: {
          maxChunkSizeBytes: serverSettings.maxChunkSizeBytes,
          supportedHashAlgorithm: serverSettings.supportedHashAlgorithm,
        },
      });

      await filesApi.updateFileContent(nodeFileId, {
        chunkHashes,
        hash: fileHash,
        contentType: "text/plain",
        name: fileName,
        nodeId: nodeFileId,
      });

      setOriginalContent(content);
      onSaved?.();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : t("preview.errors.saveFailed", { ns: "files" }),
      );
      throw err;
    } finally {
      setSaving(false);
    }
  };

  return {
    saving,
    error,
    setError,
    handleSave,
  };
};
