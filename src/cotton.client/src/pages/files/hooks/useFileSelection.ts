import { useCallback, useMemo, useRef, useState } from "react";
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
  /**
   * Toggle a single item's selection state.
   * If shiftKey is pressed and orderedIds is provided, selects a range
   * from the last clicked item to the current item.
   */
  toggleItem: (
    id: string,
    options?: { shiftKey?: boolean; orderedIds?: ReadonlyArray<string> },
  ) => void;
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
  const lastClickedIdRef = useRef<string | null>(null);

  const toggleSelectionMode = useCallback(() => {
    setSelectionMode((prev) => {
      if (prev) {
        setSelectedIds(new Set());
        lastClickedIdRef.current = null;
      }
      return !prev;
    });
  }, []);

  const toggleItem = useCallback((
    id: string,
    options?: { shiftKey?: boolean; orderedIds?: ReadonlyArray<string> },
  ) => {
    setSelectedIds((prev) => {
      const shiftKey = !!options?.shiftKey;
      const orderedIds = options?.orderedIds;

      const anchorId = lastClickedIdRef.current;

      if (shiftKey && anchorId && orderedIds && orderedIds.length > 0) {
        const anchorIndex = orderedIds.indexOf(anchorId);
        const currentIndex = orderedIds.indexOf(id);

        if (anchorIndex >= 0 && currentIndex >= 0) {
          const start = Math.min(anchorIndex, currentIndex);
          const end = Math.max(anchorIndex, currentIndex);
          const next = new Set(prev);

          for (let i = start; i <= end; i += 1) {
            const rangeId = orderedIds[i];
            if (rangeId) next.add(rangeId);
          }

          lastClickedIdRef.current = id;
          return next;
        }
      }

      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }

      lastClickedIdRef.current = id;
      return next;
    });
  }, []);

  const selectAll = useCallback((tiles: FileSystemTile[]) => {
    setSelectedIds(new Set(tiles.map(getTileId)));
    lastClickedIdRef.current = null;
  }, []);

  const deselectAll = useCallback(() => {
    setSelectedIds(new Set());
    lastClickedIdRef.current = null;
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
