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
  type FileTransferOperation,
} from "../../../shared/store/moveClipboardStore";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";
import { getSystemKeyboardShortcut } from "@shared/utils/keyboardShortcuts";
import { useFileConflictDialog } from "./useFileConflictDialog";

interface DropHandlersForGoUp {
  onDragOver: (event: React.DragEvent<HTMLElement>) => void;
  onDragLeave: (event: React.DragEvent<HTMLElement>) => void;
  onDrop: (event: React.DragEvent<HTMLElement>) => void;
  active: boolean;
}

interface DropHandlersForBreadcrumbs {
  canAccept: (targetCrumbId: string) => boolean;
  onDragOver: (
    targetCrumbId: string,
    event: React.DragEvent<HTMLElement>,
  ) => void;
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

export interface UseFileMoveControllerArgs {
  nodeId: string | null;
  tiles: ReadonlyArray<FileSystemTile>;
  selectedIds: ReadonlySet<string>;
  selectedCount: number;
  goUpParentId: string | null;
  onClipboardSet?: () => void;
  showToast: (message: string) => void;
  t: TFunction;
}

export interface UseFileMoveControllerResult {
  moveSupport: MoveSupport | undefined;
  clipboardCount: number;
  handleCutSelection: () => void;
  handleCopySelection: () => void;
  handleCopyFolder: (folderId: string) => void;
  handleCopyFile: (fileId: string) => void;
  handlePasteHere: () => void;
  handleCutFolder: (folderId: string) => void;
  handleCutFile: (fileId: string) => void;
  goUpDropHandlers: DropHandlersForGoUp | undefined;
  breadcrumbsDropHandlers: DropHandlersForBreadcrumbs;
  conflictDialog: {
    state: ReturnType<typeof useFileConflictDialog>["dialogState"];
    onResolve: ReturnType<typeof useFileConflictDialog>["handleResolve"];
    onExited: ReturnType<typeof useFileConflictDialog>["handleExited"];
  };
}

/**
 * Page-level controller that owns all move-feature glue for FilesPage:
 * clipboard population from the current selection or single-tile actions,
 * Ctrl+C / Ctrl+X / Ctrl+V hotkeys, and drop handlers for go-up and breadcrumb targets.
 * Returns thin handlers the page composes into its toolbar and layout.
 */
export const useFileMoveController = ({
  nodeId,
  tiles,
  selectedIds,
  selectedCount,
  goUpParentId,
  onClipboardSet,
  showToast,
  t,
}: UseFileMoveControllerArgs): UseFileMoveControllerResult => {
  const conflictDialog = useFileConflictDialog();
  const moveOps = useMoveOperations({
    confirmConflict: conflictDialog.showConflictDialog,
  });
  const clipboardItems = useMoveClipboardStore((s) => s.items);
  const operation = useMoveClipboardStore((s) => s.operation);
  const cutItemIds = useMemo(() => {
    switch (operation) {
      case "move":
        return new Set(clipboardItems.map((item) => item.id));
      case "copy":
        return new Set<string>();
    }
  }, [clipboardItems, operation]);

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

  const resolveFocusedItemId = useCallback(
    (target: EventTarget | null): string | null => {
      const elements: Element[] = [];

      if (target instanceof Element) {
        elements.push(target);
      }

      if (document.activeElement instanceof Element) {
        elements.push(document.activeElement);
      }

      for (const element of elements) {
        const itemElement = element.closest<HTMLElement>(
          "[data-tile-id], [data-id]",
        );
        const itemId = itemElement?.dataset.tileId ?? itemElement?.dataset.id;
        if (!itemId) continue;

        const items = buildClipboardItemsFromIds([itemId]);
        if (items.length > 0) return itemId;
      }

      return null;
    },
    [buildClipboardItemsFromIds],
  );

  const setClipboardItems = useCallback(
    (
      items: ReadonlyArray<MoveClipboardItem>,
      operation: FileTransferOperation,
    ): boolean => {
      if (items.length === 0) return false;
      switch (operation) {
        case "move":
          moveOps.cutItems(items);
          showToast(t("move.toasts.cut", { ns: "files", count: items.length }));
          break;
        case "copy":
          moveOps.copyItems(items);
          showToast(
            t("copy.toasts.ready", { ns: "files", count: items.length }),
          );
          break;
      }
      onClipboardSet?.();
      return true;
    },
    [moveOps, onClipboardSet, showToast, t],
  );

  const handleCutSelection = useCallback(() => {
    if (selectedCount === 0) return;
    const items = buildClipboardItemsFromIds(selectedIds);
    setClipboardItems(items, "move");
  }, [
    buildClipboardItemsFromIds,
    setClipboardItems,
    selectedCount,
    selectedIds,
  ]);

  const handleCopySelection = useCallback(() => {
    setClipboardItems(buildClipboardItemsFromIds(selectedIds), "copy");
  }, [buildClipboardItemsFromIds, setClipboardItems, selectedIds]);

  const handleClipboardKeyboardTarget = useCallback(
    (target: EventTarget | null, operation: FileTransferOperation): boolean => {
      if (selectedCount > 0) {
        return setClipboardItems(
          buildClipboardItemsFromIds(selectedIds),
          operation,
        );
      }

      const focusedItemId = resolveFocusedItemId(target);
      if (!focusedItemId) return false;

      return setClipboardItems(
        buildClipboardItemsFromIds([focusedItemId]),
        operation,
      );
    },
    [
      buildClipboardItemsFromIds,
      setClipboardItems,
      resolveFocusedItemId,
      selectedCount,
      selectedIds,
    ],
  );

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

  useEffect(() => {
    const isEditableTarget = (target: EventTarget | null): boolean => {
      if (!(target instanceof HTMLElement)) return false;
      if (target.isContentEditable) return true;
      const tag = target.tagName;
      return tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT";
    };

    const handler = (event: KeyboardEvent) => {
      const shortcut = getSystemKeyboardShortcut(event, [
        "cut",
        "copy",
        "paste",
      ]);
      if (!shortcut) return;
      if (isEditableTarget(event.target)) return;

      switch (shortcut) {
        case "cut":
          if (handleClipboardKeyboardTarget(event.target, "move")) {
            event.preventDefault();
          }
          break;
        case "copy":
          if (handleClipboardKeyboardTarget(event.target, "copy")) {
            event.preventDefault();
          }
          break;
        case "paste":
          if (clipboardItems.length > 0 && nodeId) {
            event.preventDefault();
            handlePasteHere();
          }
          break;
      }
    };

    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [
    clipboardItems.length,
    handleClipboardKeyboardTarget,
    handlePasteHere,
    nodeId,
  ]);

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
        const related = event.relatedTarget;
        if (related !== null && !(related instanceof Node)) return;
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

  const breadcrumbsDropHandlers = useMemo<DropHandlersForBreadcrumbs>(
    () => ({
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
    }),
    [canAcceptDropOn, handleMoveItems, nodeId],
  );

  const moveSupport = useMemo<MoveSupport | undefined>(() => {
    if (!nodeId) return undefined;
    return {
      cutItemIds,
      currentParentId: nodeId,
      onMove: handleMoveItems,
    };
  }, [cutItemIds, handleMoveItems, nodeId]);

  const handleCutItem = useCallback(
    (id: string) => {
      setClipboardItems(buildClipboardItemsFromIds([id]), "move");
    },
    [buildClipboardItemsFromIds, setClipboardItems],
  );
  const handleCopyItem = useCallback(
    (id: string) => {
      setClipboardItems(buildClipboardItemsFromIds([id]), "copy");
    },
    [buildClipboardItemsFromIds, setClipboardItems],
  );

  return {
    moveSupport,
    clipboardCount: clipboardItems.length,
    handleCutSelection,
    handlePasteHere,
    handleCutFolder: handleCutItem,
    handleCutFile: handleCutItem,
    handleCopyFile: handleCopyItem,
    handleCopyFolder: handleCopyItem,
    handleCopySelection,
    goUpDropHandlers,
    breadcrumbsDropHandlers,
    conflictDialog: {
      state: conflictDialog.dialogState,
      onResolve: conflictDialog.handleResolve,
      onExited: conflictDialog.handleExited,
    },
  };
};
