import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "react-toastify";
import { filesApi } from "../api/filesApi";
import { nodesApi } from "../api/nodesApi";
import type { NodeDto } from "../api/layoutsApi";
import { isAxiosError } from "../api/httpClient";
import { fetchServerSettings } from "../api/queries/serverSettings";
import { queryClient } from "../api/queries/queryClient";
import { refreshNodeContent } from "../store/nodesActions";
import {
  useMoveClipboardStore,
  type MoveClipboardItem,
} from "../store/moveClipboardStore";
import { useNodesStore } from "../store/nodesStore";
import {
  ClientEncryptionSizeLimitError,
  assertClientEncryptionBlobPipelineSize,
  getFolderEncryptionPolicyStateFromParentResolver,
  isFileEncrypted,
  useVault,
} from "../crypto";
import {
  decryptExistingFileWithTask,
  encryptExistingFileWithTask,
} from "../tasks";
import { showActionToast } from "../ui/ActionToast";
import { formatBytes } from "../utils/formatBytes";

/**
 * Non-authoritative drag hint type. Used so synchronous drag-over handlers can
 * cheaply detect "this is a move drag" and read which source-parent(s) the drag
 * came from without parsing JSON. The server still revalidates everything.
 */
export const MOVE_DRAG_DATA_TYPE = "application/x-cotton-move";

/**
 * Per-item marker prefix. Encodes each dragged item's id as a fake MIME suffix
 * so drag-over can synchronously reject drops onto the items themselves
 * (e.g. dragging folder F onto itself). `DataTransfer.getData()` is restricted
 * during dragenter/dragover for security; `DataTransfer.types` is always readable.
 */
const MOVE_DRAG_ITEM_TYPE = "application/x-cotton-move-item";

/**
 * Authoritative drag payload type. The drop handler is the only consumer.
 */
export const MOVE_DRAG_DATA_MIME = "application/x-cotton-move-items";

export interface MoveDragPayload {
  items: ReadonlyArray<MoveClipboardItem>;
}

/**
 * Normalize an id for drag-marker comparisons. Browsers lowercase the MIME
 * `type` string anyway, so writing the suffix upper-case and reading it back
 * via `DataTransfer.types` would silently miss. We always compare lower-case.
 */
const normalizeDragId = (id: string): string => id.toLowerCase();

export const writeMoveDragPayload = (
  dataTransfer: DataTransfer,
  payload: MoveDragPayload,
): void => {
  dataTransfer.effectAllowed = "move";

  // Tag the drag with source-parent IDs so drag-over can synchronously reject
  // drops onto the source folder without parsing the payload. UI hint only.
  const sources = new Set(payload.items.map((i) => normalizeDragId(i.sourceParentId)));
  for (const source of sources) {
    dataTransfer.setData(`${MOVE_DRAG_DATA_TYPE}/${source}`, "1");
  }
  dataTransfer.setData(MOVE_DRAG_DATA_TYPE, "1");

  // Same trick for per-item IDs so drag-over can reject dropping a folder onto itself.
  for (const item of payload.items) {
    dataTransfer.setData(`${MOVE_DRAG_ITEM_TYPE}/${normalizeDragId(item.id)}`, "1");
  }

  try {
    dataTransfer.setData(
      MOVE_DRAG_DATA_MIME,
      JSON.stringify({ items: payload.items }),
    );
  } catch {
    // Some browsers refuse non-text data on DataTransfer; the marker types
    // still let drop handlers detect a move drag, just without payload.
  }
};

/**
 * True if the drag's source-parent set contains the given id (case-insensitive).
 * Prefer over `getMoveDragSourceParents().has(...)` from callers — handles the
 * mixed-case GUID gotcha that browser MIME-type lowercasing creates.
 */
export const moveDragHasSourceParent = (
  dataTransfer: DataTransfer | null,
  parentId: string,
): boolean => getMoveDragSourceParents(dataTransfer).has(normalizeDragId(parentId));

/**
 * True if the drag includes the given id as one of its items (case-insensitive).
 */
export const moveDragHasItem = (
  dataTransfer: DataTransfer | null,
  itemId: string,
): boolean => getMoveDragItemIds(dataTransfer).has(normalizeDragId(itemId));

/**
 * Strip items that would be a no-op or invalid for a move into `targetParentId`:
 * items already inside the target, and the target itself. Case-insensitive — see
 * `normalizeDragId` for the GUID-casing rationale.
 */
export const filterMoveItemsForTarget = (
  items: ReadonlyArray<MoveClipboardItem>,
  targetParentId: string,
): MoveClipboardItem[] => {
  const target = normalizeDragId(targetParentId);
  return items.filter(
    (item) =>
      normalizeDragId(item.id) !== target &&
      normalizeDragId(item.sourceParentId) !== target,
  );
};

export const isMoveDrag = (dataTransfer: DataTransfer | null): boolean => {
  if (!dataTransfer) return false;
  const types = dataTransfer.types;
  if (!types) return false;
  for (const type of Array.from(types)) {
    if (type === MOVE_DRAG_DATA_TYPE) return true;
    if (type.startsWith(`${MOVE_DRAG_DATA_TYPE}/`)) return true;
  }
  return false;
};

export const getMoveDragSourceParents = (
  dataTransfer: DataTransfer | null,
): ReadonlySet<string> => {
  const result = new Set<string>();
  if (!dataTransfer) return result;
  for (const type of Array.from(dataTransfer.types ?? [])) {
    if (type.startsWith(`${MOVE_DRAG_DATA_TYPE}/`)) {
      result.add(type.slice(MOVE_DRAG_DATA_TYPE.length + 1));
    }
  }
  return result;
};

/**
 * Returns the set of dragged item IDs as recorded by writeMoveDragPayload.
 * Safe to call during dragenter/dragover (does not read JSON payload).
 */
export const getMoveDragItemIds = (
  dataTransfer: DataTransfer | null,
): ReadonlySet<string> => {
  const result = new Set<string>();
  if (!dataTransfer) return result;
  for (const type of Array.from(dataTransfer.types ?? [])) {
    if (type.startsWith(`${MOVE_DRAG_ITEM_TYPE}/`)) {
      result.add(type.slice(MOVE_DRAG_ITEM_TYPE.length + 1));
    }
  }
  return result;
};

export const readMoveDragPayload = (
  dataTransfer: DataTransfer | null,
): MoveDragPayload | null => {
  if (!dataTransfer) return null;
  const raw = dataTransfer.getData(MOVE_DRAG_DATA_MIME);
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as MoveDragPayload;
    if (!parsed || !Array.isArray(parsed.items)) return null;
    return parsed;
  } catch {
    return null;
  }
};

const extractErrorMessage = (error: unknown): string | null => {
  if (!isAxiosError(error)) return null;
  const data = error.response?.data;
  if (data && typeof data === "object") {
    const maybe = data as { message?: unknown };
    if (typeof maybe.message === "string" && maybe.message.length > 0) {
      return maybe.message;
    }
  }
  return null;
};

const findCachedNode = (nodeId: string): NodeDto | null => {
  const state = useNodesStore.getState();

  if (state.currentNode?.id === nodeId) {
    return state.currentNode;
  }

  const ancestor = state.ancestors.find((node) => node.id === nodeId);
  if (ancestor) {
    return ancestor;
  }

  for (const content of Object.values(state.contentByNodeId)) {
    const node = content?.nodes.find((item) => item.id === nodeId);
    if (node) {
      return node;
    }
  }

  return null;
};

const getCachedFolderEncryptionPolicyEnabled = (nodeId: string): boolean => {
  const node = findCachedNode(nodeId);
  if (!node) return false;

  return getFolderEncryptionPolicyStateFromParentResolver(
    node,
    findCachedNode,
  ).effectiveEnabled;
};

const needsEncryptionAfterMove = (item: MoveClipboardItem): boolean =>
  item.kind === "file" &&
  item.file !== undefined &&
  !isFileEncrypted(item.file.metadata);

const needsDecryptionAfterMove = (item: MoveClipboardItem): boolean =>
  item.kind === "file" &&
  item.file !== undefined &&
  isFileEncrypted(item.file.metadata);

interface MoveOutcome {
  succeeded: ReadonlyArray<MoveClipboardItem>;
  failed: ReadonlyArray<MoveClipboardItem>;
  lastErrorMessage: string | null;
}

interface UseMoveOperationsResult {
  cutItems: (items: ReadonlyArray<MoveClipboardItem>) => void;
  clearClipboard: () => void;
  /** Paste current clipboard contents into the target parent. */
  pasteInto: (targetParentId: string) => Promise<void>;
  /** Move arbitrary items (e.g. from drag-and-drop) into the target parent. */
  moveItems: (
    items: ReadonlyArray<MoveClipboardItem>,
    targetParentId: string,
  ) => Promise<void>;
}

export const useMoveOperations = (): UseMoveOperationsResult => {
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
      // Only filter the truly impossible case (a folder dropped on itself).
      // Do NOT filter by item.sourceParentId === target — that field is
      // captured at cut-time and goes stale if another window/client moves
      // the entity in the meantime; a paste back into the original folder
      // would then be silently skipped on the client even though the server
      // could perform a valid move. Visual drop-target rejection still uses
      // the stricter `filterMoveItemsForTarget` in TilesView/ListView.
      const target = normalizeDragId(targetParentId);
      const candidates = items.filter((item) => normalizeDragId(item.id) !== target);
      if (candidates.length === 0) {
        return { succeeded: [], failed: [], lastErrorMessage: null };
      }

      const targetNode = findCachedNode(targetParentId);
      const targetEncryptsNewFiles =
        getCachedFolderEncryptionPolicyEnabled(targetParentId);
      const filesToEncrypt = targetEncryptsNewFiles
        ? candidates.filter(needsEncryptionAfterMove)
        : [];
      let encryptionServerSettings: Awaited<
        ReturnType<typeof fetchServerSettings>
      > | null = null;

      if (filesToEncrypt.length > 0) {
        if (!useVault.getState().isUnlocked) {
          const message = t("common:clientEncryption.vaultLockedForDownload");
          toast.error(message);
          return {
            succeeded: [],
            failed: candidates,
            lastErrorMessage: message,
          };
        }

        try {
          encryptionServerSettings = await fetchServerSettings(queryClient);
        } catch {
          const message = t("errors.serverSettingsNotLoaded", {
            ns: "tasks",
          });
          toast.error(message);
          return {
            succeeded: [],
            failed: candidates,
            lastErrorMessage: message,
          };
        }

        try {
          for (const item of filesToEncrypt) {
            assertClientEncryptionBlobPipelineSize(
              item.file?.sizeBytes ?? 0,
              "encrypt",
            );
          }
        } catch (error) {
          const message =
            error instanceof ClientEncryptionSizeLimitError
              ? t("errors.clientEncryptionFileTooLarge", {
                  ns: "tasks",
                  maxSize: formatBytes(error.maxBytes),
                })
              : t("move.toasts.failed", {
                  ns: "files",
                  count: filesToEncrypt.length,
                });
          toast.error(message);
          return {
            succeeded: [],
            failed: candidates,
            lastErrorMessage: message,
          };
        }
      }

      const sourceParents = new Set<string>();
      const succeeded: MoveClipboardItem[] = [];
      const failed: MoveClipboardItem[] = [];
      const movedFilesToEncrypt: MoveClipboardItem[] = [];
      const movedFilesToOfferDecrypt: MoveClipboardItem[] = [];
      let lastErrorMessage: string | null = null;

      // Serial loop: the server's collision/cycle checks are pre-update reads,
      // so concurrent moves can still race in the small window before the unique
      // index throws. Serial keeps the multi-item UX deterministic.
      for (const item of candidates) {
        try {
          if (item.kind === "folder") {
            await nodesApi.moveNode(item.id, { parentId: targetParentId });
          } else {
            await filesApi.moveFile(item.id, { parentId: targetParentId });
          }
          sourceParents.add(item.sourceParentId);
          succeeded.push(item);
          if (targetEncryptsNewFiles && needsEncryptionAfterMove(item)) {
            movedFilesToEncrypt.push(item);
          }
          if (!targetEncryptsNewFiles && needsDecryptionAfterMove(item)) {
            movedFilesToOfferDecrypt.push(item);
          }
        } catch (error) {
          failed.push(item);
          lastErrorMessage = extractErrorMessage(error) ?? lastErrorMessage;
          console.error(`Failed to move ${item.kind} ${item.id}`, error);
        }
      }

      // Always refresh both source(s) and target so caches stay correct.
      const parentsToRefresh = new Set<string>(sourceParents);
      parentsToRefresh.add(targetParentId);
      for (const id of parentsToRefresh) {
        void refreshNodeContent(id);
      }

      if (movedFilesToEncrypt.length > 0) {
        const settings = encryptionServerSettings;
        if (!settings) return { succeeded, failed, lastErrorMessage };

        for (const item of movedFilesToEncrypt) {
          if (!item.file) continue;

          try {
            await encryptExistingFileWithTask({
              file: {
                id: item.id,
                name: item.file.name,
                contentType: item.file.contentType,
                sizeBytes: item.file.sizeBytes,
              },
              targetNodeId: targetParentId,
              scopeLabel: targetNode?.name ?? "",
              server: {
                maxChunkSizeBytes: settings.maxChunkSizeBytes,
                supportedHashAlgorithm: settings.supportedHashAlgorithm,
              },
            });
          } catch (error) {
            failed.push(item);
            lastErrorMessage =
              error instanceof Error ? error.message : lastErrorMessage;
          }
        }

        void refreshNodeContent(targetParentId);
      }

      if (movedFilesToOfferDecrypt.length > 0) {
        showActionToast({
          toastId: `files-cse-decrypt-moved-${targetParentId}-${Date.now()}`,
          message: t("clientEncryption.movedEncrypted.toast", {
            ns: "files",
            count: movedFilesToOfferDecrypt.length,
          }),
          action: t("clientEncryption.movedEncrypted.action", { ns: "files" }),
          onAction: () => {
            void decryptMovedEncryptedFiles({
              files: movedFilesToOfferDecrypt,
              targetParentId,
              targetNodeName: targetNode?.name ?? "",
              t,
            });
          },
        });
      }

      if (succeeded.length > 0) {
        toast.success(
          t("move.toasts.moved", { ns: "files", count: succeeded.length }),
          { toastId: `move-success-${targetParentId}-${Date.now()}` },
        );
      }
      if (failed.length > 0) {
        toast.error(
          lastErrorMessage ??
            t("move.toasts.failed", { ns: "files", count: failed.length }),
          { toastId: `move-error-${targetParentId}-${Date.now()}` },
        );
      }

      return { succeeded, failed, lastErrorMessage };
    },
    [t],
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

      // Only clear the clipboard after settle. If some items failed (most likely
      // a name collision in the target), keep the failed items in the clipboard
      // so the user can retry into a different target without re-selecting.
      if (outcome.failed.length === 0) {
        clear();
      } else {
        useMoveClipboardStore.getState().setItems(outcome.failed);
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

async function decryptMovedEncryptedFiles(options: {
  files: ReadonlyArray<MoveClipboardItem>;
  targetParentId: string;
  targetNodeName: string;
  t: ReturnType<typeof useTranslation<["files", "common", "tasks"]>>["t"];
}): Promise<void> {
  const { files, targetParentId, targetNodeName, t } = options;

  if (!useVault.getState().isUnlocked) {
    toast.error(t("clientEncryption.toasts.unlockRequired", { ns: "files" }));
    return;
  }

  let settings: Awaited<ReturnType<typeof fetchServerSettings>>;
  try {
    settings = await fetchServerSettings(queryClient);
  } catch {
    toast.error(t("errors.serverSettingsNotLoaded", { ns: "tasks" }));
    return;
  }

  let decryptedCount = 0;
  let failedCount = 0;

  for (const item of files) {
    if (!item.file) continue;

    try {
      await decryptExistingFileWithTask({
        file: {
          id: item.id,
          name: item.file.name,
          contentType: item.file.contentType,
          sizeBytes: item.file.sizeBytes,
          metadata: item.file.metadata,
        },
        targetNodeId: targetParentId,
        scopeLabel: targetNodeName,
        server: {
          maxChunkSizeBytes: settings.maxChunkSizeBytes,
          supportedHashAlgorithm: settings.supportedHashAlgorithm,
        },
      });
      decryptedCount += 1;
    } catch {
      failedCount += 1;
    }
  }

  void refreshNodeContent(targetParentId);

  if (decryptedCount > 0) {
    toast.success(
      t("clientEncryption.toasts.decryptExistingComplete", {
        ns: "files",
        count: decryptedCount,
      }),
    );
  }

  if (failedCount > 0) {
    toast.error(
      t("clientEncryption.toasts.decryptExistingFailed", {
        ns: "files",
        count: failedCount,
      }),
    );
  }
}
