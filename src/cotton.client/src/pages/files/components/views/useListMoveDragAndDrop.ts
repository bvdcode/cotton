import React, { useCallback, useRef, useState } from "react";
import type { GridRowClassNameParams } from "@mui/x-data-grid";
import type { IFileListView } from "@shared/types/FileListViewTypes";
import type { FileListRow } from "./fileListColumns";
import {
  isMoveDrag,
  moveDragHasSourceParent,
  moveDragHasItem,
  writeMoveDragPayload,
  readMoveDragPayload,
  filterMoveItemsForTarget,
} from "../../../../shared/hooks/useMoveOperations";
import { setClippedDragImage } from "./dragPreview";
import type { MoveClipboardItem } from "../../../../shared/store/moveClipboardStore";

interface ListMoveDragOptions {
  rowsById: Map<string, FileListRow>;
  moveSupport: IFileListView["moveSupport"];
  selectionMode: boolean;
  selectedIds: IFileListView["selectedIds"];
  folderOperations: IFileListView["folderOperations"];
  fileOperations: IFileListView["fileOperations"];
}

export const useListMoveDragAndDrop = ({
  rowsById,
  moveSupport,
  selectionMode,
  selectedIds,
  folderOperations,
  fileOperations,
}: ListMoveDragOptions) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [dropTargetId, setDropTargetId] = useState<string | null>(null);

  const findRowElement = useCallback(
    (target: EventTarget | null): HTMLElement | null => {
      if (!(target instanceof Element)) return null;
      return target.closest<HTMLElement>("[data-id]");
    },
    [],
  );

  const isInlineRenameTarget = useCallback(
    (target: EventTarget | null): boolean => {
      if (!(target instanceof Element)) return false;
      return Boolean(
        target.closest("input, textarea, [contenteditable='true']"),
      );
    },
    [],
  );

  const isRowRenaming = useCallback(
    (row: FileListRow): boolean => {
      if (row.type === "folder") {
        return folderOperations.isRenaming(String(row.id));
      }

      if (row.type === "file") {
        return fileOperations.isRenaming(String(row.id));
      }

      return false;
    },
    [fileOperations, folderOperations],
  );

  const buildDragPayloadForRow = useCallback(
    (rowId: string): ReadonlyArray<MoveClipboardItem> | null => {
      if (!moveSupport) return null;
      const currentParentId = moveSupport.currentParentId;
      if (!currentParentId) return null;

      const row = rowsById.get(rowId);
      if (!row) return null;

      const rowToItem = (r: FileListRow): MoveClipboardItem | null => {
        if (r.type === "folder") {
          return {
            id: String(r.id),
            kind: "folder",
            sourceParentId: currentParentId,
          };
        }
        if (r.type === "file") {
          return {
            id: String(r.id),
            kind: "file",
            sourceParentId: r.containerNodeId ?? currentParentId,
            file: {
              name: r.name,
              contentType: r.contentType ?? "application/octet-stream",
              sizeBytes: r.sizeBytes ?? 0,
              metadata: r.metadata ?? {},
            },
          };
        }
        return null;
      };

      const usingSelection =
        selectionMode &&
        selectedIds &&
        selectedIds.size > 1 &&
        selectedIds.has(rowId);

      if (usingSelection) {
        const items: MoveClipboardItem[] = [];
        for (const id of selectedIds!) {
          const candidate = rowsById.get(id);
          if (!candidate) continue;
          const item = rowToItem(candidate);
          if (item) items.push(item);
        }
        if (items.length > 0) return items;
      }

      const item = rowToItem(row);
      return item ? [item] : null;
    },
    [moveSupport, rowsById, selectedIds, selectionMode],
  );

  const handleContainerMouseDown = useCallback(
    (event: React.MouseEvent<HTMLDivElement>) => {
      if (!moveSupport) return;
      if (event.button !== 0) return;
      if (isInlineRenameTarget(event.target)) return;
      const rowEl = findRowElement(event.target);
      if (!rowEl) return;
      const rowId = rowEl.getAttribute("data-id");
      if (!rowId) return;
      const row = rowsById.get(rowId);
      if (!row || isRowRenaming(row)) return;
      // Make rows draggable on demand so non-row interactions (text selection,
      // sort headers, checkboxes) keep working normally.
      rowEl.setAttribute("draggable", "true");
    },
    [
      findRowElement,
      isInlineRenameTarget,
      isRowRenaming,
      moveSupport,
      rowsById,
    ],
  );

  const handleContainerDragStart = useCallback(
    (event: React.DragEvent<HTMLDivElement>) => {
      if (!moveSupport) return;
      const rowEl = findRowElement(event.target);
      if (!rowEl) return;
      const rowId = rowEl.getAttribute("data-id");
      if (!rowId) return;
      const row = rowsById.get(rowId);
      if (!row || isRowRenaming(row)) {
        event.preventDefault();
        return;
      }

      const items = buildDragPayloadForRow(rowId);
      if (!items || items.length === 0) {
        event.preventDefault();
        return;
      }
      writeMoveDragPayload(event.dataTransfer, { items });
      setClippedDragImage(event, rowEl);
    },
    [
      buildDragPayloadForRow,
      findRowElement,
      isRowRenaming,
      moveSupport,
      rowsById,
    ],
  );

  const handleContainerDragOver = useCallback(
    (event: React.DragEvent<HTMLDivElement>) => {
      if (!moveSupport) return;
      if (!isMoveDrag(event.dataTransfer)) return;
      const rowEl = findRowElement(event.target);
      if (!rowEl) {
        if (dropTargetId !== null) setDropTargetId(null);
        return;
      }
      const rowId = rowEl.getAttribute("data-id");
      if (!rowId) return;
      const row = rowsById.get(rowId);
      if (!row || row.type !== "folder") {
        if (dropTargetId !== null) setDropTargetId(null);
        return;
      }
      // Reject early: (a) folder cannot be a drop target for items already inside it,
      // (b) a folder cannot be dropped onto itself.
      if (moveDragHasSourceParent(event.dataTransfer, rowId)) return;
      if (moveDragHasItem(event.dataTransfer, rowId)) return;

      event.preventDefault();
      event.dataTransfer.dropEffect = "move";
      if (dropTargetId !== rowId) {
        setDropTargetId(rowId);
      }
    },
    [dropTargetId, findRowElement, moveSupport, rowsById],
  );

  const handleContainerDragLeave = useCallback(
    (event: React.DragEvent<HTMLDivElement>) => {
      if (!moveSupport) return;
      const host = containerRef.current;
      if (!host) return;
      const related = event.relatedTarget;
      if (related !== null && !(related instanceof Node)) return;
      if (related && host.contains(related)) return;
      setDropTargetId(null);
    },
    [moveSupport],
  );

  const handleContainerDrop = useCallback(
    (event: React.DragEvent<HTMLDivElement>) => {
      if (!moveSupport) return;
      if (!isMoveDrag(event.dataTransfer)) return;
      const rowEl = findRowElement(event.target);
      if (!rowEl) return;
      const rowId = rowEl.getAttribute("data-id");
      if (!rowId) return;
      const row = rowsById.get(rowId);
      if (!row || row.type !== "folder") return;

      event.preventDefault();
      event.stopPropagation();
      setDropTargetId(null);

      const payload = readMoveDragPayload(event.dataTransfer);
      if (!payload) return;
      const filtered = filterMoveItemsForTarget(payload.items, rowId);
      if (filtered.length === 0) return;
      moveSupport.onMove(filtered, rowId);
    },
    [findRowElement, moveSupport, rowsById],
  );

  const cutItemIds = moveSupport?.cutItemIds;

  const getRowClassName = useCallback(
    (params: GridRowClassNameParams<FileListRow>) => {
      const classes: string[] = [];
      const idStr = String(params.id);
      if (cutItemIds?.has(idStr)) classes.push("cotton-row-cut");
      if (dropTargetId === idStr) classes.push("cotton-row-drop");
      return classes.join(" ");
    },
    [cutItemIds, dropTargetId],
  );

  return {
    containerRef,
    getRowClassName,
    handleContainerMouseDown,
    handleContainerDragStart,
    handleContainerDragOver,
    handleContainerDragLeave,
    handleContainerDrop,
  };
};
