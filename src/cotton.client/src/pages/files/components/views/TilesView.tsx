import React, { useEffect, useMemo, useRef, useState } from "react";
import { Box, Typography, useMediaQuery } from "@mui/material";
import { Virtuoso } from "react-virtuoso";
import type { IFileListView, FileSystemTile } from "../../types/FileListViewTypes";
import { useTheme } from "@mui/material/styles";
import Loader from "../../../../shared/ui/Loader";
import { TileItem, NewFolderCard } from "./TileItem";

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
  const [containerWidth, setContainerWidth] = useState<number>(0);
  const [scrollParent, setScrollParent] = useState<HTMLElement | null>(null);

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
    <Box ref={containerRef} position="relative" pb={{ xs: 1, sm: 2 }}>
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
                {rowTiles.map((tile: FileSystemTile) => {
                  const tileId = tile.kind === "folder" ? tile.node.id : tile.file.id;
                  return (
                    <TileItem
                      key={tileId}
                      tile={tile}
                      folderOperations={folderOperations}
                      fileOperations={fileOperations}
                      fileNamePlaceholder={fileNamePlaceholder}
                      selectionMode={selectionMode}
                      selected={selectedIds?.has(tileId)}
                      onToggle={onToggleItem ? () => onToggleItem(tileId) : undefined}
                    />
                  );
                })}
              </Box>
            );
          }}
        />
      ) : (
        <Box sx={gridStyles}>
          {tiles.map((tile: FileSystemTile) => {
            const tileId = tile.kind === "folder" ? tile.node.id : tile.file.id;
            return (
              <TileItem
                key={tileId}
                tile={tile}
                folderOperations={folderOperations}
                fileOperations={fileOperations}
                fileNamePlaceholder={fileNamePlaceholder}
                selectionMode={selectionMode}
                selected={selectedIds?.has(tileId)}
                onToggle={onToggleItem ? () => onToggleItem(tileId) : undefined}
              />
            );
          })}
        </Box>
      )}
    </Box>
  );
};
