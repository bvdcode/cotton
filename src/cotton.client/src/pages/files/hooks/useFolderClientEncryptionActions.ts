import { reportClientError } from "@shared/utils/clientDiagnostics";
import { useCallback, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { fetchServerSettings } from "../../../shared/api/queries/serverSettings";
import { queryClient } from "../../../shared/api/queries/queryClient";
import type { NodeDto } from "../../../shared/api/layoutsApi";
import type { NodeContentDto } from "../../../shared/api/nodesApi";
import type { ServerSettings } from "../../../shared/api/settingsApi";
import {
  isFileEncrypted,
  isFolderEncryptionPolicyEnabled,
  useVault,
} from "../../../shared/crypto";
import { refreshNodeContent } from "../../../shared/store/nodesActions";
import {
  collectEncryptedFilesInFoldersForClientEncryption,
  collectPlainFilesInFoldersForClientEncryption,
  type ClientEncryptionFolderScanResult,
} from "../../../shared/utils/clientEncryptionFolderScan";
import {
  decryptExistingFileWithTask,
  encryptExistingFileWithTask,
  taskManager,
} from "../../../shared/tasks";

type FileOperation = "encrypt" | "decrypt";

interface UseFolderClientEncryptionActionsOptions {
  nodeId: string | null;
  currentNode: NodeDto | null;
  content: NodeContentDto | undefined;
  folderPolicyEnabled?: boolean;
  onToast: (message: string, variant?: "info" | "error") => void;
}

const getOperation = (kind: FileOperation) => {
  switch (kind) {
    case "encrypt":
      return {
        scan: collectPlainFilesInFoldersForClientEncryption,
        execute: encryptExistingFileWithTask,
        complete: "clientEncryption.toasts.encryptExistingComplete",
        failed: "clientEncryption.toasts.encryptExistingFailed",
        incomplete: "clientEncryption.toasts.encryptExistingScanIncomplete",
      } as const;
    case "decrypt":
      return {
        scan: collectEncryptedFilesInFoldersForClientEncryption,
        execute: decryptExistingFileWithTask,
        complete: "clientEncryption.toasts.decryptExistingComplete",
        failed: "clientEncryption.toasts.decryptExistingFailed",
        incomplete: "clientEncryption.toasts.decryptExistingScanIncomplete",
      } as const;
  }
};

export const useFolderClientEncryptionActions = ({
  nodeId,
  currentNode,
  content,
  folderPolicyEnabled: providedFolderPolicyEnabled,
  onToast,
}: UseFolderClientEncryptionActionsOptions) => {
  const { t } = useTranslation(["files", "tasks"]);
  const [running, setRunning] = useState({ encrypt: false, decrypt: false });
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

  const run = useCallback(
    async (kind: FileOperation): Promise<void> => {
      if (!nodeId || !activeNode) {
        return;
      }
      if (!useVault.getState().isUnlocked) {
        onToast(
          t("clientEncryption.toasts.unlockRequired", { ns: "files" }),
          "error",
        );
        return;
      }
      const operation = getOperation(kind);
      setRunning((state) => ({ ...state, [kind]: true }));
      try {
        let settings: ServerSettings;
        try {
          settings = await fetchServerSettings(queryClient);
        } catch (error) {
          reportClientError(
            "Failed to load settings for file operation",
            error,
          );
          onToast(
            t("errors.serverSettingsNotLoaded", { ns: "tasks" }),
            "error",
          );
          return;
        }

        let scan: ClientEncryptionFolderScanResult;
        try {
          scan = await operation.scan([nodeId]);
        } catch (error) {
          reportClientError("Failed to scan folder for file operation", error);
          onToast(t(operation.incomplete, { ns: "files" }), "error");
          return;
        }

        const server = {
          maxChunkSizeBytes: settings.maxChunkSizeBytes,
          supportedHashAlgorithm: settings.supportedHashAlgorithm,
        };
        const tasks = scan.files.map((file) =>
          taskManager.createTask({
            kind,
            label: file.name,
            scopeLabel: activeNode.name,
            bytesTotal: file.sizeBytes,
          }),
        );
        const refreshedParents = new Set<string>([nodeId]);
        let completed = 0;
        let failed = 0;
        for (const [index, file] of scan.files.entries()) {
          try {
            await operation.execute({
              file,
              targetNodeId: file.nodeId,
              scopeLabel: activeNode.name,
              server,
              task: tasks[index],
            });
            refreshedParents.add(file.nodeId);
            completed += 1;
          } catch (error) {
            reportClientError("Folder file operation failed", error);
            failed += 1;
          }
        }

        if (completed > 0) {
          onToast(t(operation.complete, { ns: "files", count: completed }));
        }
        if (failed > 0) {
          onToast(t(operation.failed, { ns: "files", count: failed }), "error");
        }
        if (scan.truncated) {
          onToast(t(operation.incomplete, { ns: "files" }), "error");
        }

        try {
          await Promise.all(
            [...refreshedParents].map((parentId) =>
              refreshNodeContent(parentId),
            ),
          );
        } catch (error) {
          reportClientError(
            "Failed to refresh folders after file operation",
            error,
          );
          onToast(t("errors.loadContentsFailed", { ns: "files" }), "error");
        }
      } finally {
        setRunning((state) => ({ ...state, [kind]: false }));
      }
    },
    [activeNode, nodeId, onToast, t],
  );

  const encryptPlainFiles = useCallback(() => run("encrypt"), [run]);
  const decryptEncryptedFiles = useCallback(() => run("decrypt"), [run]);

  return {
    folderPolicyEnabled,
    plainFiles,
    encryptedFiles,
    isEncryptingPlainFiles: running.encrypt,
    isDecryptingEncryptedFiles: running.decrypt,
    encryptPlainFiles,
    decryptEncryptedFiles,
  };
};
