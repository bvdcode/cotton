import type { MoveClipboardItem } from "../store/moveClipboardStore";
import { isJsonObject, type JsonValue } from "../types/json";

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

const isMoveClipboardItem = (value: JsonValue): value is MoveClipboardItem & JsonValue => {
  if (!isJsonObject(value)) return false;
  if (
    typeof value.id !== "string" ||
    (value.kind !== "folder" && value.kind !== "file") ||
    typeof value.sourceParentId !== "string"
  ) {
    return false;
  }

  if (value.file === undefined) return true;
  return (
    isJsonObject(value.file) &&
    typeof value.file.name === "string" &&
    typeof value.file.contentType === "string" &&
    typeof value.file.sizeBytes === "number" &&
    isJsonObject(value.file.metadata) &&
    Object.values(value.file.metadata).every(
      (entry) => typeof entry === "string",
    )
  );
};

const isMoveDragPayload = (value: JsonValue): value is MoveDragPayload & JsonValue =>
  isJsonObject(value) &&
  Array.isArray(value.items) &&
  value.items.every(isMoveClipboardItem);

/**
 * Normalize an id for drag-marker comparisons. Browsers lowercase the MIME
 * `type` string anyway, so writing the suffix upper-case and reading it back
 * via `DataTransfer.types` would silently miss. We always compare lower-case.
 */
export const normalizeDragId = (id: string): string => id.toLowerCase();

export const writeMoveDragPayload = (
  dataTransfer: DataTransfer,
  payload: MoveDragPayload,
): void => {
  dataTransfer.effectAllowed = "move";

  // Tag the drag with source-parent IDs so drag-over can synchronously reject
  // drops onto the source folder without parsing the payload. UI hint only.
  const sources = new Set(
    payload.items.map((i) => normalizeDragId(i.sourceParentId)),
  );
  for (const source of sources) {
    dataTransfer.setData(`${MOVE_DRAG_DATA_TYPE}/${source}`, "1");
  }
  dataTransfer.setData(MOVE_DRAG_DATA_TYPE, "1");

  // Same trick for per-item IDs so drag-over can reject dropping a folder onto itself.
  for (const item of payload.items) {
    dataTransfer.setData(
      `${MOVE_DRAG_ITEM_TYPE}/${normalizeDragId(item.id)}`,
      "1",
    );
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
): boolean =>
  getMoveDragSourceParents(dataTransfer).has(normalizeDragId(parentId));

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
    const parsed: JsonValue = JSON.parse(raw);
    return isMoveDragPayload(parsed) ? parsed : null;
  } catch {
    return null;
  }
};
