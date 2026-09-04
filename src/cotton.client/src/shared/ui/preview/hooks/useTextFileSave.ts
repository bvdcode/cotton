import { useState } from "react";
import type { Guid } from "../../../api/layoutsApi";
import type { NodeFileManifestDto } from "../../../api/nodesApi";
import { isFileEncrypted } from "../../../crypto";
import { uploadFileToNode } from "../../../upload/uploadFileToNode";
import { useServerSettings } from "../../../store/useServerSettings";
import { useTranslation } from "react-i18next";

export const useTextFileSave = (
  nodeFileId: Guid,
  fileName: string,
  originalContent: string,
  setOriginalContent: (content: string) => void,
  onSaved?: () => void,
  sourceFile?: NodeFileManifestDto | null,
) => {
  const { t } = useTranslation(["files"]);
  const { data: serverSettings } = useServerSettings();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSave = async (content: string) => {
    if (content === originalContent || !serverSettings) return;

    try {
      setSaving(true);
      setError(null);

      const file = new File([content], fileName, {
        type: sourceFile?.contentType || "text/plain",
      });
      await uploadFileToNode({
        file,
        nodeId: sourceFile?.nodeId ?? nodeFileId,
        replaceNodeFileId: nodeFileId,
        server: {
          maxChunkSizeBytes: serverSettings.maxChunkSizeBytes,
          supportedHashAlgorithm: serverSettings.supportedHashAlgorithm,
        },
        encrypt: isFileEncrypted(sourceFile?.metadata),
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
