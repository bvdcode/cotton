import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import type { NodeDto } from "../../../shared/api/layoutsApi";
import { nodesApi } from "../../../shared/api/nodesApi";
import { FOLDER_ENCRYPTION_POLICY_KEY } from "../../../shared/crypto";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { collectFoldersInFoldersForClientEncryption } from "../../../shared/utils/clientEncryptionFolderScan";

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
        const updatedNodes = await updateFolderEncryptionPolicyTree(
          folderId,
          nextEnabled,
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

const updateFolderEncryptionPolicyTree = async (
  folderId: string,
  nextEnabled: boolean,
): Promise<NodeDto[]> => {
  const value = String(nextEnabled);
  const scan = await collectFoldersInFoldersForClientEncryption([folderId]);
  const foldersToUpdate = [
    folderId,
    ...scan.folders
      .filter(
        (folder) => folder.metadata?.[FOLDER_ENCRYPTION_POLICY_KEY] !== value,
      )
      .map((folder) => folder.id),
  ];
  const updatedNodes: NodeDto[] = [];

  for (const nodeId of foldersToUpdate) {
    const updatedNode = await nodesApi.updateNodeMetadata(nodeId, {
      [FOLDER_ENCRYPTION_POLICY_KEY]: value,
    });
    updatedNodes.push(updatedNode);
  }

  return updatedNodes;
};
