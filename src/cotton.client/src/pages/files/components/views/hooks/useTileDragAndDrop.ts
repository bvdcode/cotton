import { useCallback, useMemo, useState } from "react";
import type { DragEvent } from "react";
import type {
  FileSystemTile,
  IFileListView,
} from "@shared/types/FileListViewTypes";
import {
  filterMoveItemsForTarget,
  isMoveDrag,
  moveDragHasItem,
  moveDragHasSourceParent,
  readMoveDragPayload,
  writeMoveDragPayload,
} from "@shared/hooks/useMoveOperations";
import type { MoveClipboardItem } from "@shared/store/moveClipboardStore";

type MoveSupport = IFileListView["moveSupport"];

interface UseTileDragAndDropArgs {
  tiles: FileSystemTile[];
  moveSupport: MoveSupport;
}

export const useTileDragAndDrop = ({
  tiles,
  moveSupport,
}: UseTileDragAndDropArgs) => {
  const [dropTargetId, setDropTargetId] = useState<string | null>(null);

  const tilesById = useMemo(() => {
    const map = new Map<string, FileSystemTile>();
    for (const tile of tiles) {
      const tileId = tile.kind === "folder" ? tile.node.id : tile.file.id;
      map.set(tileId, tile);
    }
    return map;
  }, [tiles]);

  const buildDragPayload = useCallback(
    (sourceTileId: string): ReadonlyArray<MoveClipboardItem> | null => {
      if (!moveSupport) return null;
      const currentParentId = moveSupport.currentParentId;
      if (!currentParentId) return null;

      const tile = tilesById.get(sourceTileId);
      if (!tile) return null;
      if (tile.kind === "folder") {
        return [
          {
            id: tile.node.id,
            kind: "folder",
            sourceParentId: tile.node.parentId ?? currentParentId,
          },
        ];
      }
      return [
        {
          id: tile.file.id,
          kind: "file",
          sourceParentId: tile.file.nodeId ?? currentParentId,
          file: {
            name: tile.file.name,
            contentType: tile.file.contentType,
            sizeBytes: tile.file.sizeBytes,
            metadata: "metadata" in tile.file ? tile.file.metadata : {},
          },
        },
      ];
    },
    [moveSupport, tilesById],
  );

  const handleMoveDragStart = useCallback(
    (tileId: string, event: DragEvent<HTMLDivElement>) => {
      if (!moveSupport) return;
      const items = buildDragPayload(tileId);
      if (!items || items.length === 0) {
        event.preventDefault();
        return;
      }
      writeMoveDragPayload(event.dataTransfer, { items });
    },
    [buildDragPayload, moveSupport],
  );

  const handleMoveDragOver = useCallback(
    (tileId: string, event: DragEvent<HTMLDivElement>) => {
      if (!moveSupport) return;
      if (!isMoveDrag(event.dataTransfer)) return;

      if (moveDragHasSourceParent(event.dataTransfer, tileId)) return;
      if (moveDragHasItem(event.dataTransfer, tileId)) return;

      event.preventDefault();
      event.stopPropagation();
      event.dataTransfer.dropEffect = "move";
      if (dropTargetId !== tileId) {
        setDropTargetId(tileId);
      }
    },
    [dropTargetId, moveSupport],
  );

  const handleMoveDragLeave = useCallback(
    (tileId: string, event: DragEvent<HTMLDivElement>) => {
      if (!moveSupport) return;
      const related = event.relatedTarget as Node | null;
      if (related && event.currentTarget.contains(related)) return;
      if (dropTargetId === tileId) {
        setDropTargetId(null);
      }
    },
    [dropTargetId, moveSupport],
  );

  const handleMoveDrop = useCallback(
    (tileId: string, event: DragEvent<HTMLDivElement>) => {
      if (!moveSupport) return;
      if (!isMoveDrag(event.dataTransfer)) return;
      event.preventDefault();
      event.stopPropagation();
      setDropTargetId(null);

      const payload = readMoveDragPayload(event.dataTransfer);
      if (!payload) return;
      const filtered = filterMoveItemsForTarget(payload.items, tileId);
      if (filtered.length === 0) return;
      moveSupport.onMove(filtered, tileId);
    },
    [moveSupport],
  );

  return {
    cutItemIds: moveSupport?.cutItemIds,
    dropTargetId,
    handleMoveDragStart,
    handleMoveDragOver,
    handleMoveDragLeave,
    handleMoveDrop,
  };
};
