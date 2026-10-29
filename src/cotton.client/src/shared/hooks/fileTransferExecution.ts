import { FolderTransfer } from "../move/FolderTransfer";
import { reportClientError } from "@shared/utils/clientDiagnostics";
import { toast } from "@shared/ui/notifications";
import type { NodeFileManifestDto } from "../api/nodesApi";
import { fetchServerSettings } from "../api/queries/serverSettings";
import { queryClient } from "../api/queries/queryClient";
import { refreshNodeContent } from "../store/nodesActions";
import type {
  MoveClipboardItem,
  FileTransferOperation,
} from "../store/moveClipboardStore";
import { useVault } from "../crypto";
import { encryptExistingFileWithTask } from "../tasks";
import { collectPlainFilesInFoldersForClientEncryption } from "../utils/clientEncryptionFolderScan";
import { extractErrorMessage } from "./moveState";
import { normalizeDragId } from "./moveDragPayload";
import type {
  MoveExecutionResult,
  UseMoveOperationsOptions,
  EncryptionCandidate,
  EncryptionCandidateScanResult,
  MoveTranslation,
} from "./moveTypes";

export const getMoveCandidates = (
  items: ReadonlyArray<MoveClipboardItem>,
  targetParentId: string,
): MoveClipboardItem[] => {
  // Only filter the truly impossible case (a folder dropped on itself).
  // Do NOT filter by item.sourceParentId === target — that field is captured
  // at cut-time and can go stale if another window/client moves the entity.
  const target = normalizeDragId(targetParentId);
  return items.filter((item) => normalizeDragId(item.id) !== target);
};

export const loadEncryptionServerSettings = async (
  shouldLoad: boolean,
): Promise<Awaited<ReturnType<typeof fetchServerSettings>> | null> => {
  if (!shouldLoad) return null;
  if (!useVault.getState().isUnlocked) return null;

  try {
    return await fetchServerSettings(queryClient);
  } catch {
    return null;
  }
};

export const moveCandidatesToTarget = async (options: {
  operation: FileTransferOperation;
  candidates: ReadonlyArray<MoveClipboardItem>;
  confirmConflict: UseMoveOperationsOptions["confirmConflict"];
  targetEncryptsNewFiles: boolean;
  targetParentId: string;
  onProgress?: (processed: number) => void;
  prepareEncryptedTarget?: () => Promise<void>;
}): Promise<MoveExecutionResult> => {
  const transfer = new FolderTransfer(
    options.operation,
    options.confirmConflict,
    options.prepareEncryptedTarget,
  );
  const succeeded: MoveClipboardItem[] = [];
  const failed: MoveClipboardItem[] = [];
  const notMoved: MoveClipboardItem[] = [];
  let lastErrorMessage: string | null = null;
  for (const [index, item] of options.candidates.entries()) {
    const outcome = await transfer.execute(
      item,
      options.targetParentId,
      options.targetEncryptsNewFiles,
      index < options.candidates.length - 1,
    );
    switch (outcome.kind) {
      case "completed":
        succeeded.push(outcome.item);
        break;
      case "failed":
        failed.push(item);
        notMoved.push(item);
        lastErrorMessage =
          extractErrorMessage(outcome.error) ?? lastErrorMessage;
        reportClientError(
          "Failed to transfer " + item.kind + " " + item.id,
          outcome.error,
        );
        break;
      case "skipped":
        notMoved.push(item);
        break;
      case "cancelled":
        notMoved.push(...options.candidates.slice(index));
        return {
          ...transfer.result,
          succeeded,
          failed,
          notMoved,
          lastErrorMessage,
        };
    }
    options.onProgress?.(index + 1);
  }
  return { ...transfer.result, succeeded, failed, notMoved, lastErrorMessage };
};

export const refreshMovedParents = (
  sourceParents: ReadonlySet<string>,
  targetParentId: string,
): void => {
  const parentsToRefresh = new Set<string>(sourceParents);
  parentsToRefresh.add(targetParentId);

  for (const id of parentsToRefresh) {
    void refreshNodeContent(id);
  }
};

export const encryptMovedFiles = async (options: {
  files: ReadonlyArray<EncryptionCandidate>;
  settings: Awaited<ReturnType<typeof fetchServerSettings>> | null;
  targetNodeName: string;
  targetParentId: string;
  t: MoveTranslation;
}): Promise<number> => {
  if (options.files.length === 0) return 0;

  if (!options.settings) {
    if (useVault.getState().isUnlocked) {
      toast.error(options.t("errors.serverSettingsNotLoaded", { ns: "tasks" }));
    }
    void refreshNodeContent(options.targetParentId);
    return 0;
  }

  let failedCount = 0;
  const refreshedParents = new Set<string>([options.targetParentId]);

  for (const item of options.files) {
    try {
      await encryptExistingFileWithTask({
        file: item.file,
        targetNodeId: item.targetNodeId,
        scopeLabel: options.targetNodeName,
        server: {
          maxChunkSizeBytes: options.settings.maxChunkSizeBytes,
          supportedHashAlgorithm: options.settings.supportedHashAlgorithm,
        },
      });
      refreshedParents.add(item.targetNodeId);
    } catch {
      failedCount += 1;
    }
  }

  for (const parentId of refreshedParents) {
    void refreshNodeContent(parentId);
  }
  return failedCount;
};

export const toDirectEncryptionCandidate = (
  item: MoveClipboardItem,
  targetParentId: string,
): EncryptionCandidate | null => {
  if (!item.file) return null;

  return {
    file: {
      id: item.id,
      name: item.file.name,
      contentType: item.file.contentType,
      sizeBytes: item.file.sizeBytes,
    },
    targetNodeId: targetParentId,
  };
};

const toNestedEncryptionCandidate = (
  file: NodeFileManifestDto,
): EncryptionCandidate => ({
  file: {
    id: file.id,
    name: file.name,
    contentType: file.contentType,
    sizeBytes: file.sizeBytes,
  },
  targetNodeId: file.nodeId,
});

export const collectMovedFolderEncryptionCandidates = async (
  folders: ReadonlyArray<MoveClipboardItem>,
): Promise<EncryptionCandidateScanResult> => {
  if (folders.length === 0) {
    return { candidates: [], incomplete: false };
  }

  try {
    const scan = await collectPlainFilesInFoldersForClientEncryption(
      folders.map((folder) => folder.id),
    );

    return {
      candidates: scan.files.map(toNestedEncryptionCandidate),
      incomplete: scan.truncated,
    };
  } catch (error) {
    reportClientError("Failed to scan moved folders for plain files", error);
    return { candidates: [], incomplete: true };
  }
};
