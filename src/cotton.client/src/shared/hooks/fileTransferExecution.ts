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
import { useNodesStore } from "../store/nodesStore";
import { useVault } from "../crypto";
import { encryptExistingFileWithTask } from "../tasks";
import { collectPlainFilesInFoldersForClientEncryption } from "../utils/clientEncryptionFolderScan";
import {
  moveItemWithConflictResolution,
  type MoveSingleItemResult,
} from "../move/moveConflictResolution";
import {
  extractErrorMessage,
  needsEncryptionAfterMove,
  needsDecryptionAfterMove,
} from "./moveState";
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
}): Promise<MoveExecutionResult> => {
  const sourceParents = new Set<string>();
  const succeeded: MoveClipboardItem[] = [];
  const failed: MoveClipboardItem[] = [];
  const notMoved: MoveClipboardItem[] = [];
  const movedFilesToEncrypt: MoveClipboardItem[] = [];
  const movedFilesToOfferDecrypt: MoveClipboardItem[] = [];
  let lastErrorMessage: string | null = null;
  let skipAllConflicts = false;

  // Serial loop: the server's collision/cycle checks are pre-update reads,
  // so concurrent moves can still race in the small window before the unique
  // index throws. Serial keeps the multi-item UX deterministic.
  for (let index = 0; index < options.candidates.length; index += 1) {
    const item = options.candidates[index];
    const outcome = await moveItemWithConflictResolution({
      operation: options.operation,
      confirmConflict: options.confirmConflict,
      item,
      skipAllConflicts,
      targetParentId: options.targetParentId,
    });

    switch (outcome.kind) {
      case "moved": {
        const transferred = applyTransferToCache(
          item,
          outcome.moved,
          options.targetParentId,
          options.operation,
        );
        if (options.operation === "move") {
          sourceParents.add(item.sourceParentId);
        }
        succeeded.push(transferred);
        collectMovedEncryptionFollowups({
          item: transferred,
          movedFilesToEncrypt,
          movedFilesToOfferDecrypt,
          targetEncryptsNewFiles: options.targetEncryptsNewFiles,
        });
        break;
      }
      case "failed":
        failed.push(item);
        notMoved.push(item);
        lastErrorMessage =
          extractErrorMessage(outcome.error) ?? lastErrorMessage;
        reportClientError(
          "Failed to move " + item.kind + " " + item.id,
          outcome.error,
        );
        break;
      case "skipped":
        notMoved.push(item);
        skipAllConflicts ||= outcome.skipAll;
        break;
      case "cancelled":
        notMoved.push(...options.candidates.slice(index));
        index = options.candidates.length;
        break;
    }
  }

  return {
    failed,
    lastErrorMessage,
    movedFilesToEncrypt,
    movedFilesToOfferDecrypt,
    notMoved,
    sourceParents,
    succeeded,
  };
};

const applyTransferToCache = (
  item: MoveClipboardItem,
  moved: MoveSingleItemResult,
  targetParentId: string,
  operation: FileTransferOperation,
): MoveClipboardItem => {
  const store = useNodesStore.getState();
  if (moved.kind === "folder") {
    switch (operation) {
      case "move":
        store.moveFolderInCache(
          moved.folder,
          item.sourceParentId,
          targetParentId,
        );
        return item;
      case "copy":
        store.addFolderToCache(targetParentId, moved.folder);
        return { ...item, id: moved.folder.id, sourceParentId: targetParentId };
    }
  }
  switch (operation) {
    case "move":
      store.moveFileInCache(moved.file, item.sourceParentId, targetParentId);
      return item;
    case "copy":
      store.upsertFileInCache(targetParentId, moved.file);
      return {
        ...item,
        id: moved.file.id,
        sourceParentId: targetParentId,
        file: moved.file,
      };
  }
};

const collectMovedEncryptionFollowups = (options: {
  item: MoveClipboardItem;
  movedFilesToEncrypt: MoveClipboardItem[];
  movedFilesToOfferDecrypt: MoveClipboardItem[];
  targetEncryptsNewFiles: boolean;
}): void => {
  if (
    options.targetEncryptsNewFiles &&
    needsEncryptionAfterMove(options.item)
  ) {
    options.movedFilesToEncrypt.push(options.item);
    return;
  }

  if (
    !options.targetEncryptsNewFiles &&
    needsDecryptionAfterMove(options.item)
  ) {
    options.movedFilesToOfferDecrypt.push(options.item);
  }
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
