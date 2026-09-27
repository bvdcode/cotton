import { reportClientError } from "@shared/utils/clientDiagnostics";
import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "@shared/ui/notifications";
import type { NodeFileManifestDto } from "../api/nodesApi";
import { fetchServerSettings } from "../api/queries/serverSettings";
import { queryClient } from "../api/queries/queryClient";
import { refreshNodeContent } from "../store/nodesActions";
import {
  useMoveClipboardStore,
  type MoveClipboardItem,
} from "../store/moveClipboardStore";
import { useNodesStore } from "../store/nodesStore";
import { useVault } from "../crypto";
import { encryptExistingFileWithTask } from "../tasks";
import { collectPlainFilesInFoldersForClientEncryption } from "../utils/clientEncryptionFolderScan";
import {
  moveItemWithConflictResolution,
  type MoveSingleItemResult,
} from "../move/moveConflictResolution";
export {
  MOVE_DRAG_DATA_TYPE,
  MOVE_DRAG_DATA_MIME,
  writeMoveDragPayload,
  moveDragHasSourceParent,
  moveDragHasItem,
  filterMoveItemsForTarget,
  isMoveDrag,
  getMoveDragSourceParents,
  getMoveDragItemIds,
  readMoveDragPayload,
} from "./moveDragPayload";
export type { MoveDragPayload } from "./moveDragPayload";

import {
  extractErrorMessage,
  findCachedNode,
  getCachedFolderEncryptionPolicyEnabled,
  needsEncryptionAfterMove,
  needsDecryptionAfterMove,
} from "./moveState";
import { normalizeDragId } from "./moveDragPayload";

import type {
  MoveOutcome,
  MoveExecutionResult,
  UseMoveOperationsOptions,
  EncryptionCandidate,
  EncryptionCandidateScanResult,
  MoveTranslation,
  UseMoveOperationsResult,
} from "./moveTypes";
export type { MoveTranslation } from "./moveTypes";

const getMoveCandidates = (
  items: ReadonlyArray<MoveClipboardItem>,
  targetParentId: string,
): MoveClipboardItem[] => {
  // Only filter the truly impossible case (a folder dropped on itself).
  // Do NOT filter by item.sourceParentId === target — that field is captured
  // at cut-time and can go stale if another window/client moves the entity.
  const target = normalizeDragId(targetParentId);
  return items.filter((item) => normalizeDragId(item.id) !== target);
};

const loadEncryptionServerSettings = async (
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

const moveCandidatesToTarget = async (options: {
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
      confirmConflict: options.confirmConflict,
      item,
      skipAllConflicts,
      targetParentId: options.targetParentId,
    });

    switch (outcome.kind) {
      case "moved":
        applyMovedItemToCache(item, outcome.moved, options.targetParentId);
        sourceParents.add(item.sourceParentId);
        succeeded.push(item);
        collectMovedEncryptionFollowups({
          item,
          movedFilesToEncrypt,
          movedFilesToOfferDecrypt,
          targetEncryptsNewFiles: options.targetEncryptsNewFiles,
        });
        break;
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

const applyMovedItemToCache = (
  item: MoveClipboardItem,
  moved: MoveSingleItemResult,
  targetParentId: string,
): void => {
  const store = useNodesStore.getState();
  if (moved.kind === "folder") {
    store.moveFolderInCache(moved.folder, item.sourceParentId, targetParentId);
    return;
  }

  store.moveFileInCache(moved.file, item.sourceParentId, targetParentId);
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

const refreshMovedParents = (
  sourceParents: ReadonlySet<string>,
  targetParentId: string,
): void => {
  const parentsToRefresh = new Set<string>(sourceParents);
  parentsToRefresh.add(targetParentId);

  for (const id of parentsToRefresh) {
    void refreshNodeContent(id);
  }
};

const encryptMovedFiles = async (options: {
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

const toDirectEncryptionCandidate = (
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

const collectMovedFolderEncryptionCandidates = async (
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

import {
  offerDecryptForMovedFiles,
  showMoveOutcomeToasts,
} from "./moveNotifications";

export const useMoveOperations = ({
  confirmConflict,
}: UseMoveOperationsOptions): UseMoveOperationsResult => {
  const { t } = useTranslation(["files", "common", "tasks"]);
  const setItems = useMoveClipboardStore((s) => s.setItems);
  const clear = useMoveClipboardStore((s) => s.clear);

  const cutItems = useCallback(
    (items: ReadonlyArray<MoveClipboardItem>) => {
      if (items.length === 0) {
        clear();
        return;
      }
      setItems(items);
    },
    [clear, setItems],
  );

  const moveItems = useCallback(
    async (
      items: ReadonlyArray<MoveClipboardItem>,
      targetParentId: string,
    ): Promise<MoveOutcome> => {
      const candidates = getMoveCandidates(items, targetParentId);
      if (candidates.length === 0) {
        return {
          succeeded: [],
          failed: [],
          notMoved: [],
          lastErrorMessage: null,
        };
      }

      const targetNode = findCachedNode(targetParentId);
      const targetEncryptsNewFiles =
        getCachedFolderEncryptionPolicyEnabled(targetParentId);
      const hasMoveEncryptionFollowups =
        targetEncryptsNewFiles &&
        candidates.some(
          (item) => item.kind === "folder" || needsEncryptionAfterMove(item),
        );
      const encryptionServerSettings = await loadEncryptionServerSettings(
        hasMoveEncryptionFollowups,
      );
      const result = await moveCandidatesToTarget({
        candidates,
        confirmConflict,
        targetEncryptsNewFiles,
        targetParentId,
      });

      refreshMovedParents(result.sourceParents, targetParentId);

      const directEncryptionCandidates = result.movedFilesToEncrypt
        .map((item) => toDirectEncryptionCandidate(item, targetParentId))
        .filter((item): item is EncryptionCandidate => item !== null);
      const nestedEncryptionCandidateScan = encryptionServerSettings
        ? await collectMovedFolderEncryptionCandidates(
            result.succeeded.filter((item) => item.kind === "folder"),
          )
        : { candidates: [], incomplete: false };
      const encryptionFailedCount = await encryptMovedFiles({
        files: [
          ...directEncryptionCandidates,
          ...nestedEncryptionCandidateScan.candidates,
        ],
        settings: encryptionServerSettings,
        targetNodeName: targetNode?.name ?? "",
        targetParentId,
        t,
      });
      offerDecryptForMovedFiles({
        files: result.movedFilesToOfferDecrypt,
        targetNodeName: targetNode?.name ?? "",
        targetParentId,
        t,
      });
      showMoveOutcomeToasts({
        encryptionFailedCount,
        encryptionScanIncomplete: nestedEncryptionCandidateScan.incomplete,
        failed: result.failed,
        lastErrorMessage: result.lastErrorMessage,
        succeeded: result.succeeded,
        targetParentId,
        t,
      });

      return {
        succeeded: result.succeeded,
        failed: result.failed,
        notMoved: result.notMoved,
        lastErrorMessage: result.lastErrorMessage,
      };
    },
    [confirmConflict, t],
  );

  const moveItemsVoid = useCallback(
    async (
      items: ReadonlyArray<MoveClipboardItem>,
      targetParentId: string,
    ): Promise<void> => {
      await moveItems(items, targetParentId);
    },
    [moveItems],
  );

  const pasteInto = useCallback(
    async (targetParentId: string): Promise<void> => {
      const items = useMoveClipboardStore.getState().items;
      if (items.length === 0) return;

      const outcome = await moveItems(items, targetParentId);

      // Keep failed, skipped, and cancelled items available for another paste.
      if (outcome.notMoved.length === 0) {
        clear();
      } else {
        useMoveClipboardStore.getState().setItems(outcome.notMoved);
      }
    },
    [clear, moveItems],
  );

  return {
    cutItems,
    clearClipboard: clear,
    pasteInto,
    moveItems: moveItemsVoid,
  };
};
