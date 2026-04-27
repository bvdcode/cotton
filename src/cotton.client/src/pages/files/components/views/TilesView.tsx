import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Box, Typography, useMediaQuery } from "@mui/material";
import { Virtuoso, type VirtuosoHandle } from "react-virtuoso";
import type { IFileListView, FileSystemTile } from "../../types/FileListViewTypes";
import { useTheme } from "@mui/material/styles";
import Loader from "../../../../shared/ui/Loader";
import { TileItem, NewFolderCard } from "./TileItem";
import { getFileTypeInfo } from "../../utils/fileTypes";

/**
 * Returns responsive tile min-width based on tile size.
 *
 * Adjusted for mobile:
 * - small:  80px (xs) / 112px (sm+) — visibly smaller on phone
 * - medium: 112px (xs) / 152px (sm+)
 * - large:  44% on xs (guarantees max 2 columns), 208px on sm+
 */
const useTileLayout = (tileSize: "small" | "medium" | "large") => {
  const theme = useTheme();
  const isXs = useMediaQuery(theme.breakpoints.down("sm"));

  return useMemo(() => {
    if (isXs) {
      switch (tileSize) {
        case "small":
          return { minWidth: "80px", gap: 6 };
        case "medium":
          return { minWidth: "112px", gap: 8 };
        case "large":
          return { minWidth: "44%", gap: 10 };
      }
    }

    switch (tileSize) {
      case "small":
        return { minWidth: theme.spacing(14), gap: 8 };
      case "medium":
        return { minWidth: theme.spacing(19), gap: 12 };
      case "large":
        return { minWidth: theme.spacing(26), gap: 16 };
    }
  }, [tileSize, isXs, theme]);
};

const VIRTUALIZATION_THRESHOLD = 80;

const DEFAULT_COLUMNS_FALLBACK = 2;
const FOCUS_RETRY_LIMIT = 8;

const EDITABLE_TAGS = new Set(["INPUT", "TEXTAREA", "SELECT"]);

const tryParsePx = (value: string): number | null => {
  const trimmed = value.trim();
  if (!trimmed.endsWith("px")) return null;
  const parsed = Number.parseFloat(trimmed.slice(0, -2));
  return Number.isFinite(parsed) ? parsed : null;
};

const findScrollableParent = (element: HTMLElement | null): HTMLElement | null => {
  let current: HTMLElement | null = element?.parentElement ?? null;

  while (current) {
    const style = window.getComputedStyle(current);
    const overflowY = style.overflowY;
    const overflow = style.overflow;

    const isScrollable =
      overflowY === "auto" ||
      overflowY === "scroll" ||
      overflow === "auto" ||
      overflow === "scroll";

    if (isScrollable) {
      return current;
    }

    current = current.parentElement;
  }

  return null;
};

const isEditableTarget = (target: EventTarget | null): boolean => {
  if (!(target instanceof HTMLElement)) return false;
  if (target.isContentEditable) return true;
  if (EDITABLE_TAGS.has(target.tagName)) return true;
  if (target.closest("[contenteditable='true']")) return true;
  if (target.closest(".MuiInputBase-root")) return true;

  return false;
};

const getTileIndexFromTarget = (target: EventTarget | null): number | null => {
  if (!(target instanceof Element)) return null;

  const tileElement = target.closest<HTMLElement>("[data-tile-index]");
  const value = tileElement?.dataset.tileIndex;
  if (!value) return null;

  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? parsed : null;
};

const shouldIgnoreShortcutTarget = (target: EventTarget | null): boolean => {
  if (!(target instanceof Element)) return false;

  return Boolean(
    target.closest(
      ".card-menu-slot, .card-menu-button, [role='menu'], [role='menuitem'], input[type='checkbox']",
    ),
  );
};

const hasModifierKeys = (event: { altKey: boolean; ctrlKey: boolean; metaKey: boolean }): boolean =>
  event.altKey || event.ctrlKey || event.metaKey;

interface KeyboardEventLike {
  key: string;
  altKey: boolean;
  ctrlKey: boolean;
  metaKey: boolean;
  target: EventTarget | null;
  preventDefault: () => void;
  stopPropagation: () => void;
}

/**
 * TilesView Component
 *
 * Renders files/folders in a responsive grid. For large collections
 * (>80 items) uses react-virtuoso VirtuosoGrid for DOM virtualization.
 * Smaller collections render directly for simplicity.
 */
export const TilesView: React.FC<IFileListView> = ({
  tiles,
  folderOperations,
  fileOperations,
  onNavigateBack,
  readOnly = false,
  isCreatingFolder,
  newFolderName,
  onNewFolderNameChange,
  onConfirmNewFolder,
  onCancelNewFolder,
  folderNamePlaceholder,
  fileNamePlaceholder,
  emptyStateText,
  loading = false,
  loadingTitle,
  loadingCaption,
  tileSize = "medium",
  selectionMode = false,
  selectedIds,
  onToggleItem,
}) => {
  const layout = useTileLayout(tileSize);
  const theme = useTheme();
  const isXs = useMediaQuery(theme.breakpoints.down("sm"));
  const containerRef = useRef<HTMLDivElement | null>(null);
  const virtuosoRef = useRef<VirtuosoHandle | null>(null);
  const activeIndexRef = useRef<number | null>(null);
  const focusAnimationFrameRef = useRef<number | null>(null);
  const [containerWidth, setContainerWidth] = useState<number>(0);
  const [scrollParent, setScrollParent] = useState<HTMLElement | null>(null);
  const [activeIndex, setActiveIndex] = useState<number | null>(null);

  useEffect(() => {
    const el = containerRef.current;
    if (!el || typeof ResizeObserver === "undefined") return;

    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (!entry) return;
      setContainerWidth(entry.contentRect.width);
    });

    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;

    setScrollParent(findScrollableParent(el));
  }, []);

  const gridTemplateColumns = `repeat(auto-fill, minmax(${layout.minWidth}, 1fr))`;
  const gapPx = layout.gap;

  const gridStyles: React.CSSProperties = {
    display: "grid",
    gap: `${gapPx}px`,
    gridTemplateColumns,
  };

  const columns = useMemo(() => {
    if (tileSize === "large" && isXs && layout.minWidth.trim().endsWith("%")) {
      return 2;
    }

    const minWidthPx = tryParsePx(layout.minWidth);
    if (!minWidthPx || containerWidth <= 0) {
      return DEFAULT_COLUMNS_FALLBACK;
    }

    const calculated = Math.floor((containerWidth + gapPx) / (minWidthPx + gapPx));
    return Math.max(1, calculated);
  }, [containerWidth, gapPx, isXs, layout.minWidth, tileSize]);

  const shouldVirtualize = tiles.length > VIRTUALIZATION_THRESHOLD;

  const orderedIds = useMemo(
    () =>
      tiles.map((tile) =>
        tile.kind === "folder" ? tile.node.id : tile.file.id,
      ),
    [tiles],
  );

  const selectedIndex = useMemo(() => {
    if (!selectedIds || selectedIds.size === 0) return null;

    for (let index = 0; index < orderedIds.length; index += 1) {
      const id = orderedIds[index];
      if (selectedIds.has(id)) return index;
    }

    return null;
  }, [orderedIds, selectedIds]);

  const contentMarker = useMemo(() => {
    const firstId = orderedIds[0] ?? "";
    const lastId = orderedIds[orderedIds.length - 1] ?? "";

    return `${orderedIds.length}:${firstId}:${lastId}`;
  }, [orderedIds]);

  useEffect(() => {
    setActiveIndex((prev) => {
      if (tiles.length === 0) return null;
      if (prev === null) return selectedIndex ?? 0;
      return Math.min(prev, tiles.length - 1);
    });
  }, [selectedIndex, tiles.length]);

  useEffect(() => {
    activeIndexRef.current = activeIndex;
  }, [activeIndex]);

  const cancelPendingFocus = useCallback(() => {
    if (focusAnimationFrameRef.current === null) return;
    window.cancelAnimationFrame(focusAnimationFrameRef.current);
    focusAnimationFrameRef.current = null;
  }, []);

  useEffect(() => {
    return () => {
      cancelPendingFocus();
    };
  }, [cancelPendingFocus]);

  const focusTileByIndex = useCallback(
    (rawIndex: number, options?: { activate?: boolean }) => {
      if (tiles.length === 0) return;

      const nextIndex = Math.max(0, Math.min(rawIndex, tiles.length - 1));
      setActiveIndex(nextIndex);

      if (shouldVirtualize) {
        virtuosoRef.current?.scrollToIndex({
          index: Math.floor(nextIndex / columns),
          align: "center",
          behavior: "auto",
        });
      }

      let attempts = 0;
      const tryFocus = () => {
        const host = containerRef.current;
        if (!host) return;

        const tileElement = host.querySelector<HTMLElement>(
          `[data-tile-index="${nextIndex}"]`,
        );

        if (!tileElement) {
          if (attempts < FOCUS_RETRY_LIMIT) {
            attempts += 1;
            focusAnimationFrameRef.current = window.requestAnimationFrame(tryFocus);
          }
          return;
        }

        const focusTarget =
          tileElement.querySelector<HTMLElement>(
            "[role='button'][tabindex='0']",
          ) ??
          tileElement.querySelector<HTMLElement>("input[type='checkbox']") ??
          tileElement;

        focusTarget.focus();

        if (options?.activate) {
          focusTarget.click();
        }

        focusAnimationFrameRef.current = null;
      };

      cancelPendingFocus();
      focusAnimationFrameRef.current = window.requestAnimationFrame(tryFocus);
    },
    [cancelPendingFocus, columns, shouldVirtualize, tiles.length],
  );

  useEffect(() => {
    if (loading || tiles.length === 0) return;

    const host = containerRef.current;
    if (!host) return;

    const activeElement = document.activeElement;
    if (activeElement instanceof HTMLElement) {
      if (host.contains(activeElement)) return;
      if (isEditableTarget(activeElement)) return;
    }

    const indexToFocus = activeIndexRef.current ?? selectedIndex ?? 0;
    focusTileByIndex(indexToFocus);
  }, [contentMarker, focusTileByIndex, loading, selectedIndex, tiles.length]);

  const resolveCurrentIndex = useCallback(
    (target: EventTarget | null): number | null => {
      if (tiles.length === 0) return null;

      const focusedTileIndex = getTileIndexFromTarget(target);
      if (focusedTileIndex !== null) return focusedTileIndex;

      if (activeIndexRef.current !== null) return activeIndexRef.current;
      if (selectedIndex !== null) return selectedIndex;

      return 0;
    },
    [selectedIndex, tiles.length],
  );

  const activateTileByIndex = useCallback(
    (index: number) => {
      const tile = tiles[index];
      if (!tile) return;

      if (tile.kind === "folder") {
        folderOperations.onClick(tile.node.id);
        return;
      }

      const typeInfo = getFileTypeInfo(tile.file.name, tile.file.contentType ?? null);
      if ((typeInfo.type === "image" || typeInfo.type === "video") && fileOperations.onMediaClick) {
        fileOperations.onMediaClick(tile.file.id);
        return;
      }

      fileOperations.onClick(tile.file.id, tile.file.name, tile.file.sizeBytes);
    },
    [fileOperations, folderOperations, tiles],
  );

  const renameTileByIndex = useCallback(
    (index: number) => {
      if (readOnly) return;

      const tile = tiles[index];
      if (!tile) return;

      if (tile.kind === "folder") {
        folderOperations.onStartRename?.(tile.node.id, tile.node.name);
        return;
      }

      fileOperations.onStartRename?.(tile.file.id, tile.file.name);
    },
    [fileOperations, folderOperations, readOnly, tiles],
  );

  const deleteTileByIndex = useCallback(
    (index: number) => {
      if (readOnly) return;

      const tile = tiles[index];
      if (!tile) return;

      if (tile.kind === "folder") {
        folderOperations.onDelete?.(tile.node.id, tile.node.name);
        return;
      }

      fileOperations.onDelete?.(tile.file.id, tile.file.name);
    },
    [fileOperations, folderOperations, readOnly, tiles],
  );

  const handleKeyboardEvent = useCallback(
    (event: KeyboardEventLike) => {
      if (tiles.length === 0) return;
      if (hasModifierKeys(event)) return;
      if (isEditableTarget(event.target)) return;
      if (shouldIgnoreShortcutTarget(event.target)) return;

      if (event.key === "Backspace") {
        if (!onNavigateBack) return;

        event.preventDefault();
        event.stopPropagation();
        onNavigateBack();
        return;
      }

      const currentIndex = resolveCurrentIndex(event.target);
      if (currentIndex === null) return;

      if (event.key === "F2") {
        event.preventDefault();
        event.stopPropagation();
        renameTileByIndex(currentIndex);
        return;
      }

      if (event.key === "Delete") {
        event.preventDefault();
        event.stopPropagation();
        deleteTileByIndex(currentIndex);
        return;
      }

      if (event.key === "Enter") {
        const focusedTileIndex = getTileIndexFromTarget(event.target);

        if (focusedTileIndex !== null) {
          return;
        }

        event.preventDefault();
        event.stopPropagation();

        focusTileByIndex(currentIndex);
        activateTileByIndex(currentIndex);
        return;
      }

      let delta: number | null = null;
      switch (event.key) {
        case "ArrowLeft":
          delta = -1;
          break;
        case "ArrowRight":
          delta = 1;
          break;
        case "ArrowUp":
          delta = -columns;
          break;
        case "ArrowDown":
          delta = columns;
          break;
        default:
          return;
      }

      event.preventDefault();
      event.stopPropagation();
      focusTileByIndex(currentIndex + delta);
    },
    [
      activateTileByIndex,
      columns,
      deleteTileByIndex,
      focusTileByIndex,
      onNavigateBack,
      renameTileByIndex,
      resolveCurrentIndex,
      tiles.length,
    ],
  );

  useEffect(() => {
    const handleWindowKeyDown = (event: KeyboardEvent) => {
      const host = containerRef.current;
      if (!host) return;

      const activeElement = document.activeElement;
      const isInsideHost = activeElement instanceof HTMLElement && host.contains(activeElement);
      const isDocumentContext =
        activeElement === null ||
        activeElement === document.body ||
        activeElement === document.documentElement;

      if (!isInsideHost && !isDocumentContext) return;

      handleKeyboardEvent(event);
    };

    window.addEventListener("keydown", handleWindowKeyDown, true);
    return () => {
      window.removeEventListener("keydown", handleWindowKeyDown, true);
    };
  }, [handleKeyboardEvent]);

  const handleFocusCapture = useCallback((event: React.FocusEvent<HTMLDivElement>) => {
    const tileIndex = getTileIndexFromTarget(event.target);
    if (tileIndex === null) return;

    setActiveIndex((prev) => (prev === tileIndex ? prev : tileIndex));
  }, []);

  const handlePointerDownCapture = useCallback(
    (event: React.PointerEvent<HTMLDivElement>) => {
      if (event.button !== 0) return;
      if (isEditableTarget(event.target)) return;

      const host = containerRef.current;
      if (!host) return;

      const tileIndex = getTileIndexFromTarget(event.target);
      if (tileIndex !== null) {
        setActiveIndex(tileIndex);

        const tileElement = host.querySelector<HTMLElement>(
          `[data-tile-index="${tileIndex}"]`,
        );
        const tileButton = tileElement?.querySelector<HTMLElement>(
          "[role='button'][tabindex='0']",
        );

        tileButton?.focus({ preventScroll: true });
      }
    },
    [],
  );

  const handleKeyDownCapture = useCallback(
    (event: React.KeyboardEvent<HTMLDivElement>) => {
      handleKeyboardEvent(event);
    },
    [handleKeyboardEvent],
  );

  const renderTile = useCallback(
    (tile: FileSystemTile, index: number) => {
      const tileId = tile.kind === "folder" ? tile.node.id : tile.file.id;

      return (
        <Box key={tileId} data-tile-index={index} data-tile-id={tileId}>
          <TileItem
            tile={tile}
            folderOperations={folderOperations}
            fileOperations={fileOperations}
            readOnly={readOnly}
            fileNamePlaceholder={fileNamePlaceholder}
            selectionMode={selectionMode}
            selected={selectedIds?.has(tileId)}
            onToggle={
              onToggleItem
                ? (shiftKey) =>
                    onToggleItem(tileId, {
                      shiftKey,
                      orderedIds,
                    })
                : undefined
            }
          />
        </Box>
      );
    },
    [
      fileNamePlaceholder,
      fileOperations,
      folderOperations,
      onToggleItem,
      orderedIds,
      readOnly,
      selectedIds,
      selectionMode,
    ],
  );

  if (!loading && !isCreatingFolder && tiles.length === 0 && emptyStateText) {
    return (
      <Box
        display="flex"
        alignItems="center"
        justifyContent="center"
        minHeight={160}
      >
        <Typography color="text.secondary">{emptyStateText}</Typography>
      </Box>
    );
  }

  return (
    <Box
      ref={containerRef}
      position="relative"
      pb={{ xs: 1, sm: 2 }}
      onPointerDownCapture={handlePointerDownCapture}
      onFocusCapture={handleFocusCapture}
      onKeyDownCapture={handleKeyDownCapture}
    >
      {loading && tiles.length === 0 && (
        <Box
          sx={{
            position: "absolute",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            minHeight: 200,
            bgcolor: "background.default",
            zIndex: 10,
          }}
        >
          <Loader title={loadingTitle} caption={loadingCaption} />
        </Box>
      )}

      {isCreatingFolder && (
        <Box sx={{ ...gridStyles, mb: `${gapPx}px` }}>
          <NewFolderCard
            newFolderName={newFolderName}
            onNewFolderNameChange={onNewFolderNameChange}
            onConfirmNewFolder={onConfirmNewFolder}
            onCancelNewFolder={onCancelNewFolder}
            folderNamePlaceholder={folderNamePlaceholder}
          />
        </Box>
      )}

      {shouldVirtualize ? (
        <Virtuoso
          ref={virtuosoRef}
          customScrollParent={scrollParent ?? undefined}
          totalCount={Math.ceil(tiles.length / columns)}
          overscan={600}
          itemContent={(rowIndex: number) => {
            const start = rowIndex * columns;
            const rowTiles = tiles.slice(start, start + columns);

            return (
              <Box
                sx={{
                  display: "grid",
                  gap: `${gapPx}px`,
                  gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))`,
                  pb: `${gapPx}px`,
                }}
              >
                {rowTiles.map((tile: FileSystemTile, tileOffset: number) =>
                  renderTile(tile, start + tileOffset),
                )}
              </Box>
            );
          }}
        />
      ) : (
        <Box sx={gridStyles}>
          {tiles.map((tile: FileSystemTile, index: number) =>
            renderTile(tile, index),
          )}
        </Box>
      )}
    </Box>
  );
};
