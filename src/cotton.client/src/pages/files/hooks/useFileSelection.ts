import { useCallback, useMemo, useState } from "react";
import type { FileSystemTile } from "../types/FileListViewTypes";

/** Extracts the unique ID from a tile (folder or file). */
const getTileId = (tile: FileSystemTile): string =>
  tile.kind === "folder" ? tile.node.id : tile.file.id;

export interface FileSelectionState {
  /** Whether selection mode is currently active. */
  selectionMode: boolean;
  /** Set of selected tile IDs. */
  selectedIds: ReadonlySet<string>;
  /** Number of currently selected items. */
  selectedCount: number;
  /** Toggle selection mode on/off. Exiting clears selection. */
  toggleSelectionMode: () => void;
  /** Toggle a single item's selection state. */
  toggleItem: (id: string) => void;
  /** Select all items from the given tiles array. */
  selectAll: (tiles: FileSystemTile[]) => void;
  /** Clear entire selection without exiting selection mode. */
  deselectAll: () => void;
  /** Check whether a specific item is selected. */
  isSelected: (id: string) => boolean;
}

/**
 * Manages multi-select state for the files page.
 * Selection is automatically cleared when selection mode is toggled off.
 */
export const useFileSelection = (): FileSelectionState => {
  const [selectionMode, setSelectionMode] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  const toggleSelectionMode = useCallback(() => {
    setSelectionMode((prev) => {
      if (prev) {
        setSelectedIds(new Set());
      }
      return !prev;
    });
  }, []);

  const toggleItem = useCallback((id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }, []);

  const selectAll = useCallback((tiles: FileSystemTile[]) => {
    setSelectedIds(new Set(tiles.map(getTileId)));
  }, []);

  const deselectAll = useCallback(() => {
    setSelectedIds(new Set());
  }, []);

  const isSelected = useCallback(
    (id: string) => selectedIds.has(id),
    [selectedIds],
  );

  const selectedCount = selectedIds.size;

  return useMemo(
    () => ({
      selectionMode,
      selectedIds,
      selectedCount,
      toggleSelectionMode,
      toggleItem,
      selectAll,
      deselectAll,
      isSelected,
    }),
    [
      selectionMode,
      selectedIds,
      selectedCount,
      toggleSelectionMode,
      toggleItem,
      selectAll,
      deselectAll,
      isSelected,
    ],
  );
};
