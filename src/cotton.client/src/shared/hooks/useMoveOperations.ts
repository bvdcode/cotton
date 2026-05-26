import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "@shared/ui/notifications";
import { filesApi } from "../api/filesApi";
import { nodesApi } from "../api/nodesApi";
import type { NodeDto } from "../api/layoutsApi";
import type { NodeFileManifestDto } from "../api/nodesApi";
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
  getFolderEncryptionPolicyStateFromParentResolver,
  isFileEncrypted,
  useVault,
} from "../crypto";
import {
  decryptExistingFileWithTask,
  encryptExistingFileWithTask,
} from "../tasks";
import { showActionToast } from "../ui/ActionToast";
import { collectPlainFilesInFoldersForClientEncryption } from "../utils/clientEncryptionFolderScan";
import type { ExistingFileEncryptionTaskFile } from "../tasks";

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

interface MoveExecutionResult extends MoveOutcome {
  movedFilesToEncrypt: ReadonlyArray<MoveClipboardItem>;
  movedFilesToOfferDecrypt: ReadonlyArray<MoveClipboardItem>;
  sourceParents: ReadonlySet<string>;
}

interface EncryptionCandidate {
  file: ExistingFileEncryptionTaskFile;
  targetNodeId: string;
}

type MoveTranslation = ReturnType<
  typeof useTranslation<["files", "common", "tasks"]>
>["t"];

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
  targetEncryptsNewFiles: boolean;
  targetParentId: string;
}): Promise<MoveExecutionResult> => {
  const sourceParents = new Set<string>();
  const succeeded: MoveClipboardItem[] = [];
  const failed: MoveClipboardItem[] = [];
  const movedFilesToEncrypt: MoveClipboardItem[] = [];
  const movedFilesToOfferDecrypt: MoveClipboardItem[] = [];
  let lastErrorMessage: string | null = null;

  // Serial loop: the server's collision/cycle checks are pre-update reads,
  // so concurrent moves can still race in the small window before the unique
  // index throws. Serial keeps the multi-item UX deterministic.
  for (const item of options.candidates) {
    try {
      await moveSingleItem(item, options.targetParentId);
      sourceParents.add(item.sourceParentId);
      succeeded.push(item);
      collectMovedEncryptionFollowups({
        item,
        movedFilesToEncrypt,
        movedFilesToOfferDecrypt,
        targetEncryptsNewFiles: options.targetEncryptsNewFiles,
      });
    } catch (error) {
      failed.push(item);
      lastErrorMessage = extractErrorMessage(error) ?? lastErrorMessage;
      console.error(
        "Failed to move " + item.kind + " " + item.id,
        error,
      );
    }
  }

  return {
    failed,
    lastErrorMessage,
    movedFilesToEncrypt,
    movedFilesToOfferDecrypt,
    sourceParents,
    succeeded,
  };
};

const moveSingleItem = async (
  item: MoveClipboardItem,
  targetParentId: string,
): Promise<void> => {
  if (item.kind === "folder") {
    await nodesApi.moveNode(item.id, { parentId: targetParentId });
    return;
  }

  await filesApi.moveFile(item.id, { parentId: targetParentId });
};

const collectMovedEncryptionFollowups = (options: {
  item: MoveClipboardItem;
  movedFilesToEncrypt: MoveClipboardItem[];
  movedFilesToOfferDecrypt: MoveClipboardItem[];
  targetEncryptsNewFiles: boolean;
}): void => {
  if (options.targetEncryptsNewFiles && needsEncryptionAfterMove(options.item)) {
    options.movedFilesToEncrypt.push(options.item);
    return;
  }

  if (!options.targetEncryptsNewFiles && needsDecryptionAfterMove(options.item)) {
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
): Promise<EncryptionCandidate[]> => {
  if (folders.length === 0) return [];

  try {
    const scan = await collectPlainFilesInFoldersForClientEncryption(
      folders.map((folder) => folder.id),
    );

    return scan.files.map(toNestedEncryptionCandidate);
  } catch (error) {
    console.error("Failed to scan moved folders for plain files", error);
    return [];
  }
};

const offerDecryptForMovedFiles = (options: {
  files: ReadonlyArray<MoveClipboardItem>;
  targetNodeName: string;
  targetParentId: string;
  t: MoveTranslation;
}): void => {
  if (options.files.length === 0) return;

  showActionToast({
    toastId:
      "files-cse-decrypt-moved-" + options.targetParentId + "-" + Date.now(),
    message: options.t("clientEncryption.movedEncrypted.toast", {
      ns: "files",
      count: options.files.length,
    }),
    action: options.t("clientEncryption.movedEncrypted.action", { ns: "files" }),
    onAction: () => {
      void decryptMovedEncryptedFiles({
        files: options.files,
        targetParentId: options.targetParentId,
        targetNodeName: options.targetNodeName,
        t: options.t,
      });
    },
  });
};

const showMoveOutcomeToasts = (options: {
  encryptionFailedCount: number;
  failed: ReadonlyArray<MoveClipboardItem>;
  lastErrorMessage: string | null;
  succeeded: ReadonlyArray<MoveClipboardItem>;
  targetParentId: string;
  t: MoveTranslation;
}): void => {
  if (options.succeeded.length > 0) {
    toast.success(
      options.t("move.toasts.moved", {
        ns: "files",
        count: options.succeeded.length,
      }),
      {
        toastId: "move-success-" + options.targetParentId + "-" + Date.now(),
      },
    );
  }

  if (options.failed.length > 0) {
    toast.error(
      options.lastErrorMessage ??
        options.t("move.toasts.failed", {
          ns: "files",
          count: options.failed.length,
        }),
      {
        toastId: "move-error-" + options.targetParentId + "-" + Date.now(),
      },
    );
  }

  if (options.encryptionFailedCount > 0) {
    toast.error(
      options.t("clientEncryption.toasts.encryptExistingFailed", {
        ns: "files",
        count: options.encryptionFailedCount,
      }),
      {
        toastId:
          "move-encrypt-error-" + options.targetParentId + "-" + Date.now(),
      },
    );
  }
};

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
      const candidates = getMoveCandidates(items, targetParentId);
      if (candidates.length === 0) {
        return { succeeded: [], failed: [], lastErrorMessage: null };
      }

      const targetNode = findCachedNode(targetParentId);
      const targetEncryptsNewFiles =
        getCachedFolderEncryptionPolicyEnabled(targetParentId);
      const hasMoveEncryptionFollowups =
        targetEncryptsNewFiles &&
        candidates.some((item) => item.kind === "folder" || needsEncryptionAfterMove(item));
      const encryptionServerSettings = await loadEncryptionServerSettings(
        hasMoveEncryptionFollowups,
      );
      const result = await moveCandidatesToTarget({
        candidates,
        targetEncryptsNewFiles,
        targetParentId,
      });

      refreshMovedParents(result.sourceParents, targetParentId);

      const directEncryptionCandidates = result.movedFilesToEncrypt
        .map((item) => toDirectEncryptionCandidate(item, targetParentId))
        .filter((item): item is EncryptionCandidate => item !== null);
      const nestedEncryptionCandidates = encryptionServerSettings
        ? await collectMovedFolderEncryptionCandidates(
            result.succeeded.filter((item) => item.kind === "folder"),
          )
        : [];
      const encryptionFailedCount = await encryptMovedFiles({
        files: [...directEncryptionCandidates, ...nestedEncryptionCandidates],
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
        failed: result.failed,
        lastErrorMessage: result.lastErrorMessage,
        succeeded: result.succeeded,
        targetParentId,
        t,
      });

      return {
        succeeded: result.succeeded,
        failed: result.failed,
        lastErrorMessage: result.lastErrorMessage,
      };
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
