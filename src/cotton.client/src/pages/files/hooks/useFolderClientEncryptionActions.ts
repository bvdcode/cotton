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
  collectEncryptedFilesInFoldersForClientEncryption,
  collectPlainFilesInFoldersForClientEncryption,
} from "../../../shared/utils/clientEncryptionFolderScan";
import {
  decryptExistingFileWithTask,
  encryptExistingFileWithTask,
  taskManager,
  type AppTaskHandle,
} from "../../../shared/tasks";

type ToastVariant = "info" | "error";

interface UseFolderClientEncryptionActionsOptions {
  nodeId: string | null;
  currentNode: NodeDto | null;
  content: NodeContentDto | undefined;
  folderPolicyEnabled?: boolean;
  onToast: (message: string, variant?: ToastVariant) => void;
}

export const useFolderClientEncryptionActions = ({
  nodeId,
  currentNode,
  content,
  folderPolicyEnabled: providedFolderPolicyEnabled,
  onToast,
}: UseFolderClientEncryptionActionsOptions) => {
  const { t } = useTranslation(["files", "tasks"]);
  const [isEncryptingPlainFiles, setIsEncryptingPlainFiles] = useState(false);
  const [isDecryptingEncryptedFiles, setIsDecryptingEncryptedFiles] =
    useState(false);

  const activeNode = nodeId && currentNode?.id === nodeId ? currentNode : null;
  const activeContent = nodeId && content?.id === nodeId ? content : undefined;
  const folderPolicyEnabled =
    providedFolderPolicyEnabled ??
    isFolderEncryptionPolicyEnabled(activeNode?.metadata);

  const plainFiles = useMemo(
    () =>
      folderPolicyEnabled
        ? (activeContent?.files.filter(
            (file) => !isFileEncrypted(file.metadata),
          ) ?? [])
        : [],
    [activeContent?.files, folderPolicyEnabled],
  );

  const encryptedFiles = useMemo(
    () =>
      activeContent?.files.filter((file) => isFileEncrypted(file.metadata)) ??
      [],
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
    let scanIncomplete = false;

    try {
      const settings = await fetchServerSettings(queryClient);
      const server = {
        maxChunkSizeBytes: settings.maxChunkSizeBytes,
        supportedHashAlgorithm: settings.supportedHashAlgorithm,
      };

      let filesToEncrypt = plainFiles;
      try {
        const scan = await collectPlainFilesInFoldersForClientEncryption([
          nodeId,
        ]);
        if (scan.truncated) {
          scanIncomplete = true;
        }
        if (scan.files.length > 0) {
          filesToEncrypt = scan.files;
        }
      } catch (error) {
        console.error("Failed to scan folder for plain files", error);
        scanIncomplete = true;
      }
      const refreshedParents = new Set<string>([nodeId]);
      const taskHandles = createExistingFileTaskHandles(
        filesToEncrypt,
        "encrypt",
        activeNode.name,
      );

      for (const [index, file] of filesToEncrypt.entries()) {
        try {
          await encryptExistingFileWithTask({
            file: toEncryptionTaskFile(file),
            targetNodeId: file.nodeId,
            scopeLabel: activeNode.name,
            server,
            task: taskHandles[index],
          });
          refreshedParents.add(file.nodeId);
          encryptedCount += 1;
        } catch {
          failedCount += 1;
        }
      }

      for (const parentId of refreshedParents) {
        void refreshNodeContent(parentId);
      }
    } catch {
      onToast(t("errors.serverSettingsNotLoaded", { ns: "tasks" }), "error");
      return;
    } finally {
      setIsEncryptingPlainFiles(false);
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

    if (scanIncomplete) {
      onToast(
        t("clientEncryption.toasts.encryptExistingScanIncomplete", {
          ns: "files",
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
    let scanIncomplete = false;

    try {
      const settings = await fetchServerSettings(queryClient);
      const server = {
        maxChunkSizeBytes: settings.maxChunkSizeBytes,
        supportedHashAlgorithm: settings.supportedHashAlgorithm,
      };

      let filesToDecrypt = encryptedFiles;
      try {
        const scan = await collectEncryptedFilesInFoldersForClientEncryption([
          nodeId,
        ]);
        if (scan.truncated) {
          scanIncomplete = true;
        }
        if (scan.files.length > 0) {
          filesToDecrypt = scan.files;
        }
      } catch (error) {
        console.error("Failed to scan folder for encrypted files", error);
        scanIncomplete = true;
      }
      const refreshedParents = new Set<string>([nodeId]);
      const taskHandles = createExistingFileTaskHandles(
        filesToDecrypt,
        "decrypt",
        activeNode.name,
      );

      for (const [index, file] of filesToDecrypt.entries()) {
        try {
          await decryptExistingFileWithTask({
            file: toDecryptionTaskFile(file),
            targetNodeId: file.nodeId,
            scopeLabel: activeNode.name,
            server,
            task: taskHandles[index],
          });
          refreshedParents.add(file.nodeId);
          decryptedCount += 1;
        } catch {
          failedCount += 1;
        }
      }

      for (const parentId of refreshedParents) {
        void refreshNodeContent(parentId);
      }
    } catch {
      onToast(t("errors.serverSettingsNotLoaded", { ns: "tasks" }), "error");
      return;
    } finally {
      setIsDecryptingEncryptedFiles(false);
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

    if (scanIncomplete) {
      onToast(
        t("clientEncryption.toasts.decryptExistingScanIncomplete", {
          ns: "files",
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

const createExistingFileTaskHandles = (
  files: ReadonlyArray<NodeFileManifestDto>,
  kind: "encrypt" | "decrypt",
  scopeLabel: string,
): AppTaskHandle[] =>
  files.map((file) =>
    taskManager.createTask({
      kind,
      label: file.name,
      scopeLabel,
      bytesTotal: file.sizeBytes,
    }),
  );
