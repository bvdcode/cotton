import { useCallback, useEffect, useMemo, useState } from "react";
import type { TFunction } from "i18next";
import {
  isMoveDrag,
  moveDragHasSourceParent,
  readMoveDragPayload,
  useMoveOperations,
} from "../../../shared/hooks/useMoveOperations";
import {
  useMoveClipboardStore,
  type MoveClipboardItem,
} from "../../../shared/store/moveClipboardStore";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";

interface DropHandlersForGoUp {
  onDragOver: (event: React.DragEvent<HTMLElement>) => void;
  onDragLeave: (event: React.DragEvent<HTMLElement>) => void;
  onDrop: (event: React.DragEvent<HTMLElement>) => void;
  active: boolean;
}

interface DropHandlersForBreadcrumbs {
  canAccept: (targetCrumbId: string) => boolean;
  onDragOver: (targetCrumbId: string, event: React.DragEvent<HTMLElement>) => void;
  onDrop: (targetCrumbId: string, event: React.DragEvent<HTMLElement>) => void;
}

interface MoveSupport {
  cutItemIds: ReadonlySet<string>;
  currentParentId: string;
  onMove: (
    items: ReadonlyArray<MoveClipboardItem>,
    targetParentId: string,
  ) => void;
}

type MoveKeyboardShortcut = "cut" | "paste";

const resolveMoveKeyboardShortcut = (
  event: KeyboardEvent,
): MoveKeyboardShortcut | null => {
  const key = event.key.toLowerCase();

  if (key === "x" || event.code === "KeyX") return "cut";
  if (key === "v" || event.code === "KeyV") return "paste";

  return null;
};

export interface UseFileMoveControllerArgs {
  nodeId: string | null;
  tiles: ReadonlyArray<FileSystemTile>;
  selectedIds: ReadonlySet<string>;
  selectedCount: number;
  goUpParentId: string | null;
  onItemsCut?: () => void;
  showToast: (message: string) => void;
  t: TFunction;
}

export interface UseFileMoveControllerResult {
  moveSupport: MoveSupport | undefined;
  clipboardCount: number;
  handleCutSelection: () => void;
  handlePasteHere: () => void;
  handleCutFolder: (folderId: string) => void;
  handleCutFile: (fileId: string) => void;
  goUpDropHandlers: DropHandlersForGoUp | undefined;
  breadcrumbsDropHandlers: DropHandlersForBreadcrumbs;
}

/**
 * Page-level controller that owns all move-feature glue for FilesPage:
 * clipboard population from the current selection or single-tile actions,
 * Ctrl+X / Ctrl+V hotkeys, and drop handlers for go-up and breadcrumb targets.
 * Returns thin handlers the page composes into its toolbar and layout.
 */
export const useFileMoveController = ({
  nodeId,
  tiles,
  selectedIds,
  selectedCount,
  goUpParentId,
  onItemsCut,
  showToast,
  t,
}: UseFileMoveControllerArgs): UseFileMoveControllerResult => {
  const moveOps = useMoveOperations();
  const clipboardItems = useMoveClipboardStore((s) => s.items);
  const cutItemIds = useMemo(
    () => new Set(clipboardItems.map((c) => c.id)),
    [clipboardItems],
  );

  const buildClipboardItemsFromIds = useCallback(
    (ids: Iterable<string>): MoveClipboardItem[] => {
      if (!nodeId) return [];
      const items: MoveClipboardItem[] = [];
      const idsSet = new Set(ids);
      for (const tile of tiles) {
        if (tile.kind === "folder") {
          if (!idsSet.has(tile.node.id)) continue;
          items.push({
            id: tile.node.id,
            kind: "folder",
            sourceParentId: tile.node.parentId ?? nodeId,
          });
        } else {
          if (!idsSet.has(tile.file.id)) continue;
          items.push({
            id: tile.file.id,
            kind: "file",
            sourceParentId: tile.file.nodeId ?? nodeId,
            file: {
              name: tile.file.name,
              contentType: tile.file.contentType,
              sizeBytes: tile.file.sizeBytes,
              metadata: "metadata" in tile.file ? tile.file.metadata : {},
            },
          });
        }
      }
      return items;
    },
    [nodeId, tiles],
  );

  const handleCutSelection = useCallback(() => {
    if (selectedCount === 0) return;
    const items = buildClipboardItemsFromIds(selectedIds);
    if (items.length === 0) return;
    moveOps.cutItems(items);
    onItemsCut?.();
    showToast(t("move.toasts.cut", { ns: "files", count: items.length }));
  }, [buildClipboardItemsFromIds, moveOps, onItemsCut, selectedCount, selectedIds, showToast, t]);

  const handlePasteHere = useCallback(() => {
    if (!nodeId) return;
    if (clipboardItems.length === 0) return;
    void moveOps.pasteInto(nodeId);
  }, [clipboardItems.length, moveOps, nodeId]);

  const handleMoveItems = useCallback(
    (items: ReadonlyArray<MoveClipboardItem>, targetParentId: string): void => {
      void moveOps.moveItems(items, targetParentId);
    },
    [moveOps],
  );

  // Ctrl+X / Ctrl+V — skip when focus is in an editable element so the user
  // can still cut/paste text in inputs/textareas/contenteditable.
  useEffect(() => {
    const isEditableTarget = (target: EventTarget | null): boolean => {
      if (!(target instanceof HTMLElement)) return false;
      if (target.isContentEditable) return true;
      const tag = target.tagName;
      return tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT";
    };

    const handler = (event: KeyboardEvent) => {
      if (!(event.ctrlKey || event.metaKey)) return;
      if (event.repeat) return;
      const shortcut = resolveMoveKeyboardShortcut(event);
      if (!shortcut) return;
      if (isEditableTarget(event.target)) return;

      if (shortcut === "cut") {
        if (selectedCount === 0) return;
        event.preventDefault();
        handleCutSelection();
      } else {
        if (clipboardItems.length === 0) return;
        if (!nodeId) return;
        event.preventDefault();
        handlePasteHere();
      }
    };

    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [clipboardItems.length, handleCutSelection, handlePasteHere, nodeId, selectedCount]);

  const [goUpDropActive, setGoUpDropActive] = useState(false);

  const canAcceptDropOn = useCallback(
    (event: React.DragEvent<HTMLElement>, targetParentId: string): boolean => {
      if (!isMoveDrag(event.dataTransfer)) return false;
      return !moveDragHasSourceParent(event.dataTransfer, targetParentId);
    },
    [],
  );

  const goUpDropHandlers = useMemo<DropHandlersForGoUp | undefined>(() => {
    if (!goUpParentId) return undefined;
    return {
      onDragOver: (event) => {
        if (!canAcceptDropOn(event, goUpParentId)) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = "move";
        if (!goUpDropActive) setGoUpDropActive(true);
      },
      onDragLeave: (event) => {
        const related = event.relatedTarget as Node | null;
        if (related && event.currentTarget.contains(related)) return;
        setGoUpDropActive(false);
      },
      onDrop: (event) => {
        setGoUpDropActive(false);
        if (!isMoveDrag(event.dataTransfer)) return;
        event.preventDefault();
        event.stopPropagation();
        const payload = readMoveDragPayload(event.dataTransfer);
        if (!payload || payload.items.length === 0) return;
        handleMoveItems(payload.items, goUpParentId);
      },
      active: goUpDropActive,
    };
  }, [canAcceptDropOn, goUpDropActive, goUpParentId, handleMoveItems]);

  const breadcrumbsDropHandlers = useMemo<DropHandlersForBreadcrumbs>(() => ({
    canAccept: (targetCrumbId) => targetCrumbId !== nodeId,
    onDragOver: (targetCrumbId, event) => {
      if (!canAcceptDropOn(event, targetCrumbId)) return;
      event.preventDefault();
      event.dataTransfer.dropEffect = "move";
    },
    onDrop: (targetCrumbId, event) => {
      if (!isMoveDrag(event.dataTransfer)) return;
      event.preventDefault();
      event.stopPropagation();
      const payload = readMoveDragPayload(event.dataTransfer);
      if (!payload || payload.items.length === 0) return;
      handleMoveItems(payload.items, targetCrumbId);
    },
  }), [canAcceptDropOn, handleMoveItems, nodeId]);

  const moveSupport = useMemo<MoveSupport | undefined>(() => {
    if (!nodeId) return undefined;
    return {
      cutItemIds,
      currentParentId: nodeId,
      onMove: handleMoveItems,
    };
  }, [cutItemIds, handleMoveItems, nodeId]);

  const handleCutFolder = useCallback(
    (folderId: string) => {
      if (!nodeId) return;
      moveOps.cutItems([{ id: folderId, kind: "folder", sourceParentId: nodeId }]);
      onItemsCut?.();
      showToast(t("move.toasts.cut", { ns: "files", count: 1 }));
    },
    [moveOps, nodeId, onItemsCut, showToast, t],
  );

  const handleCutFile = useCallback(
    (fileId: string) => {
      if (!nodeId) return;
      const tile = tiles.find(
        (item) => item.kind === "file" && item.file.id === fileId,
      );
      if (!tile || tile.kind !== "file") return;
      moveOps.cutItems([
        {
          id: fileId,
          kind: "file",
          sourceParentId: nodeId,
          file: {
            name: tile.file.name,
            contentType: tile.file.contentType,
            sizeBytes: tile.file.sizeBytes,
            metadata: "metadata" in tile.file ? tile.file.metadata : {},
          },
        },
      ]);
      onItemsCut?.();
      showToast(t("move.toasts.cut", { ns: "files", count: 1 }));
    },
    [moveOps, nodeId, onItemsCut, showToast, t, tiles],
  );

  return {
    moveSupport,
    clipboardCount: clipboardItems.length,
    handleCutSelection,
    handlePasteHere,
    handleCutFolder,
    handleCutFile,
    goUpDropHandlers,
    breadcrumbsDropHandlers,
  };
};
