import { reportClientError } from "@shared/utils/clientDiagnostics";
import { useCallback, useRef } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "@shared/ui/notifications";
import { isFileEncrypted, useVault } from "../crypto";

import {
  useMoveClipboardStore,
  type MoveClipboardItem,
  type FileTransferOperation,
} from "../store/moveClipboardStore";
import { getMoveTarget, needsEncryptionAfterMove } from "./moveState";
import type {
  EncryptionCandidate,
  MoveOutcome,
  UseMoveOperationsOptions,
  UseMoveOperationsResult,
} from "./moveTypes";
import {
  getMoveCandidates,
  loadEncryptionServerSettings,
  moveCandidatesToTarget,
  refreshMovedParents,
  encryptMovedFiles,
  toDirectEncryptionCandidate,
  collectMovedFolderEncryptionCandidates,
} from "./fileTransferExecution";
export type { MoveTranslation } from "./moveTypes";
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
  offerDecryptForMovedFiles,
  showMoveOutcomeToasts,
  getTransferToastKeys,
} from "./moveNotifications";

export const useMoveOperations = ({
  confirmConflict,
}: UseMoveOperationsOptions): UseMoveOperationsResult => {
  const { t } = useTranslation(["files", "common", "tasks"]);
  const setItems = useMoveClipboardStore((s) => s.setItems);
  const clear = useMoveClipboardStore((s) => s.clear);
  const pasting = useRef(false);
  const copyItems = useCallback(
    (items: ReadonlyArray<MoveClipboardItem>) => setItems(items, "copy"),
    [setItems],
  );

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
      operation: FileTransferOperation = "move",
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

      let target: Awaited<ReturnType<typeof getMoveTarget>>;
      try {
        target = await getMoveTarget(targetParentId);
      } catch (error) {
        reportClientError("Failed to load move target", error);
        toast.error(
          t(getTransferToastKeys(operation).failure, {
            ns: "files",
            count: candidates.length,
          }),
        );
        return {
          succeeded: [],
          failed: candidates,
          notMoved: candidates,
          lastErrorMessage: null,
        };
      }
      const targetEncryptsNewFiles = target.encryptsNewFiles;
      if (
        !useVault.getState().isUnlocked &&
        candidates.some(
          (item) =>
            item.kind === "file" && isFileEncrypted(item.file?.metadata),
        )
      ) {
        toast.error(
          t("clientEncryption.toasts.unlockRequired", { ns: "files" }),
        );
        return {
          succeeded: [],
          failed: candidates,
          notMoved: candidates,
          lastErrorMessage: null,
        };
      }
      const hasMoveEncryptionFollowups =
        targetEncryptsNewFiles &&
        candidates.some(
          (item) => item.kind === "folder" || needsEncryptionAfterMove(item),
        );
      const encryptionServerSettings = await loadEncryptionServerSettings(
        hasMoveEncryptionFollowups,
      );
      if (
        operation === "copy" &&
        hasMoveEncryptionFollowups &&
        !encryptionServerSettings
      ) {
        if (useVault.getState().isUnlocked) {
          toast.error(t("errors.serverSettingsNotLoaded", { ns: "tasks" }));
        } else {
          toast.error(
            t("clientEncryption.toasts.unlockRequired", { ns: "files" }),
          );
        }
        return {
          succeeded: [],
          failed: candidates,
          notMoved: candidates,
          lastErrorMessage: null,
        };
      }
      const result = await moveCandidatesToTarget({
        operation,
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
        targetNodeName: target.node.name,
        targetParentId,
        t,
      });
      if (operation === "move") {
        offerDecryptForMovedFiles({
          files: result.movedFilesToOfferDecrypt,
          targetNodeName: target.node.name,
          targetParentId,
          t,
        });
      }
      showMoveOutcomeToasts({
        operation,
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
      const { items, operation } = useMoveClipboardStore.getState();
      if (items.length === 0 || pasting.current) {
        return;
      }
      pasting.current = true;
      try {
        const outcome = await moveItems(items, targetParentId, operation);
        if (
          operation === "move" &&
          useMoveClipboardStore.getState().items === items
        ) {
          if (outcome.notMoved.length === 0) {
            clear();
          } else {
            useMoveClipboardStore.getState().setItems(outcome.notMoved);
          }
        }
      } finally {
        pasting.current = false;
      }
    },
    [clear, moveItems],
  );

  return {
    copyItems,
    cutItems,
    clearClipboard: clear,
    pasteInto,
    moveItems: moveItemsVoid,
  };
};
