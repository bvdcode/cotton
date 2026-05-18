import { useCallback, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { fetchServerSettings } from "../../../shared/api/queries/serverSettings";
import { queryClient } from "../../../shared/api/queries/queryClient";
import type { NodeDto } from "../../../shared/api/layoutsApi";
import type {
  NodeContentDto,
  NodeFileManifestDto,
} from "../../../shared/api/nodesApi";
import {
  isFileEncrypted,
  isFolderEncryptionPolicyEnabled,
  useVault,
} from "../../../shared/crypto";
import { refreshNodeContent } from "../../../shared/store/nodesActions";
import {
  decryptExistingFileWithTask,
  encryptExistingFileWithTask,
} from "../../../shared/tasks";

type ToastVariant = "info" | "error";

interface UseFolderClientEncryptionActionsOptions {
  nodeId: string | null;
  currentNode: NodeDto | null;
  content: NodeContentDto | undefined;
  onToast: (message: string, variant?: ToastVariant) => void;
}

export const useFolderClientEncryptionActions = ({
  nodeId,
  currentNode,
  content,
  onToast,
}: UseFolderClientEncryptionActionsOptions) => {
  const { t } = useTranslation(["files", "tasks"]);
  const [isEncryptingPlainFiles, setIsEncryptingPlainFiles] = useState(false);
  const [isDecryptingEncryptedFiles, setIsDecryptingEncryptedFiles] =
    useState(false);

  const activeNode =
    nodeId && currentNode?.id === nodeId ? currentNode : null;
  const activeContent =
    nodeId && content?.id === nodeId ? content : undefined;
  const folderPolicyEnabled = isFolderEncryptionPolicyEnabled(
    activeNode?.metadata,
  );

  const plainFiles = useMemo(
    () =>
      folderPolicyEnabled
        ? (activeContent?.files.filter((file) => !isFileEncrypted(file.metadata)) ??
          [])
        : [],
    [activeContent?.files, folderPolicyEnabled],
  );

  const encryptedFiles = useMemo(
    () =>
      activeContent?.files.filter((file) => isFileEncrypted(file.metadata)) ?? [],
    [activeContent?.files],
  );

  const encryptPlainFiles = useCallback(async (): Promise<void> => {
    if (!nodeId || !activeNode || plainFiles.length === 0) {
      return;
    }

    if (!useVault.getState().isUnlocked) {
      onToast(
        t("clientEncryption.toasts.unlockRequired", { ns: "files" }),
        "error",
      );
      return;
    }

    setIsEncryptingPlainFiles(true);

    let encryptedCount = 0;
    let failedCount = 0;

    try {
      const settings = await fetchServerSettings(queryClient);
      const server = {
        maxChunkSizeBytes: settings.maxChunkSizeBytes,
        supportedHashAlgorithm: settings.supportedHashAlgorithm,
      };

      for (const file of plainFiles) {
        try {
          await encryptExistingFileWithTask({
            file: toEncryptionTaskFile(file),
            targetNodeId: nodeId,
            scopeLabel: activeNode.name,
            server,
          });
          encryptedCount += 1;
        } catch {
          failedCount += 1;
        }
      }
    } catch {
      onToast(t("errors.serverSettingsNotLoaded", { ns: "tasks" }), "error");
      return;
    } finally {
      setIsEncryptingPlainFiles(false);
      void refreshNodeContent(nodeId);
    }

    if (encryptedCount > 0) {
      onToast(
        t("clientEncryption.toasts.encryptExistingComplete", {
          ns: "files",
          count: encryptedCount,
        }),
      );
    }

    if (failedCount > 0) {
      onToast(
        t("clientEncryption.toasts.encryptExistingFailed", {
          ns: "files",
          count: failedCount,
        }),
        "error",
      );
    }
  }, [activeNode, nodeId, onToast, plainFiles, t]);

  const decryptEncryptedFiles = useCallback(async (): Promise<void> => {
    if (!nodeId || !activeNode || encryptedFiles.length === 0) {
      return;
    }

    if (!useVault.getState().isUnlocked) {
      onToast(
        t("clientEncryption.toasts.unlockRequired", { ns: "files" }),
        "error",
      );
      return;
    }

    setIsDecryptingEncryptedFiles(true);

    let decryptedCount = 0;
    let failedCount = 0;

    try {
      const settings = await fetchServerSettings(queryClient);
      const server = {
        maxChunkSizeBytes: settings.maxChunkSizeBytes,
        supportedHashAlgorithm: settings.supportedHashAlgorithm,
      };

      for (const file of encryptedFiles) {
        try {
          await decryptExistingFileWithTask({
            file: toDecryptionTaskFile(file),
            targetNodeId: nodeId,
            scopeLabel: activeNode.name,
            server,
          });
          decryptedCount += 1;
        } catch {
          failedCount += 1;
        }
      }
    } catch {
      onToast(t("errors.serverSettingsNotLoaded", { ns: "tasks" }), "error");
      return;
    } finally {
      setIsDecryptingEncryptedFiles(false);
      void refreshNodeContent(nodeId);
    }

    if (decryptedCount > 0) {
      onToast(
        t("clientEncryption.toasts.decryptExistingComplete", {
          ns: "files",
          count: decryptedCount,
        }),
      );
    }

    if (failedCount > 0) {
      onToast(
        t("clientEncryption.toasts.decryptExistingFailed", {
          ns: "files",
          count: failedCount,
        }),
        "error",
      );
    }
  }, [activeNode, encryptedFiles, nodeId, onToast, t]);

  return {
    folderPolicyEnabled,
    plainFiles,
    encryptedFiles,
    isEncryptingPlainFiles,
    isDecryptingEncryptedFiles,
    encryptPlainFiles,
    decryptEncryptedFiles,
  };
};

const toEncryptionTaskFile = (file: NodeFileManifestDto) => ({
  id: file.id,
  name: file.name,
  contentType: file.contentType,
  sizeBytes: file.sizeBytes,
});

const toDecryptionTaskFile = (file: NodeFileManifestDto) => ({
  id: file.id,
  name: file.name,
  contentType: file.contentType,
  sizeBytes: file.sizeBytes,
  metadata: file.metadata,
});
