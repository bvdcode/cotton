import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import { nodesApi } from "../../../shared/api/nodesApi";
import { FOLDER_ENCRYPTION_POLICY_KEY } from "../../../shared/crypto";
import { useNodesStore } from "../../../shared/store/nodesStore";

interface UseFolderEncryptionPolicyOptions {
  onToast: (message: string, variant?: "info" | "error") => void;
}

export const useFolderEncryptionPolicy = ({
  onToast,
}: UseFolderEncryptionPolicyOptions) => {
  const { t } = useTranslation("files");

  const toggleFolderEncryptionPolicy = useCallback(
    async (folderId: string, currentlyEnabled: boolean): Promise<void> => {
      const nextEnabled = !currentlyEnabled;

      try {
        const updatedNodes = await nodesApi.bulkUpdateNodeMetadata(
          folderId,
          { [FOLDER_ENCRYPTION_POLICY_KEY]: String(nextEnabled) },
          true,
        );
        const store = useNodesStore.getState();
        for (const updatedNode of updatedNodes) {
          store.updateNode(updatedNode);
        }
        onToast(
          nextEnabled
            ? t("clientEncryption.toasts.policyEnabled")
            : t("clientEncryption.toasts.policyDisabled"),
        );
      } catch {
        onToast(t("clientEncryption.toasts.policyToggleFailed"), "error");
      }
    },
    [onToast, t],
  );

  return { toggleFolderEncryptionPolicy };
};
