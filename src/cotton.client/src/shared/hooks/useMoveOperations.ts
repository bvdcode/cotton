import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "react-toastify";
import { filesApi } from "../api/filesApi";
import { nodesApi } from "../api/nodesApi";
import { isAxiosError } from "../api/httpClient";
import { useNodesStore } from "../store/nodesStore";
import {
  useMoveClipboardStore,
  type MoveClipboardItem,
} from "../store/moveClipboardStore";

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

export const writeMoveDragPayload = (
  dataTransfer: DataTransfer,
  payload: MoveDragPayload,
): void => {
  dataTransfer.effectAllowed = "move";

  // Tag the drag with source-parent IDs so drag-over can synchronously reject
  // drops onto the source folder without parsing the payload. UI hint only.
  const sources = new Set(payload.items.map((i) => i.sourceParentId));
  for (const source of sources) {
    dataTransfer.setData(`${MOVE_DRAG_DATA_TYPE}/${source}`, "1");
  }
  dataTransfer.setData(MOVE_DRAG_DATA_TYPE, "1");

  // Same trick for per-item IDs so drag-over can reject dropping a folder onto itself.
  for (const item of payload.items) {
    dataTransfer.setData(`${MOVE_DRAG_ITEM_TYPE}/${item.id}`, "1");
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
  const { t } = useTranslation(["files", "common"]);
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
      const candidates = items.filter(
        (item) =>
          item.sourceParentId !== targetParentId && item.id !== targetParentId,
      );
      if (candidates.length === 0) {
        return { succeeded: [], failed: [], lastErrorMessage: null };
      }

      const store = useNodesStore.getState();
      const sourceParents = new Set<string>();
      const succeeded: MoveClipboardItem[] = [];
      const failed: MoveClipboardItem[] = [];
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
        void store.refreshNodeContent(id);
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
