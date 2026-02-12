import React, { useMemo, useCallback } from "react";
import { Box, Typography, useMediaQuery } from "@mui/material";
import { VirtuosoGrid } from "react-virtuoso";
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
}) => {
  const layout = useTileLayout(tileSize);

  const gridTemplateColumns = `repeat(auto-fill, minmax(${layout.minWidth}, 1fr))`;
  const gapPx = layout.gap;

  const gridStyles: React.CSSProperties = {
    display: "grid",
    gap: `${gapPx}px`,
    gridTemplateColumns,
  };

  const renderTile = useCallback(
    (index: number) => {
      const tile = tiles[index];
      if (!tile) return null;
      return (
        <TileItem
          tile={tile}
          folderOperations={folderOperations}
          fileOperations={fileOperations}
          fileNamePlaceholder={fileNamePlaceholder}
        />
      );
    },
    [tiles, folderOperations, fileOperations, fileNamePlaceholder],
  );

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

  /**
   * VirtuosoGrid List wrapper.
   * Keep CSS Grid rules consistent with the non-virtualized view.
   * Preserve Virtuoso inline styles (paddingTop/paddingBottom) that control
   * scroll-height virtualization.
   */
  const listComponent = useMemo(
    () =>
      React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
        function VirtuosoList({ children, style, ...props }, ref) {
          return (
            <div
              ref={ref}
              {...props}
              style={{ ...gridStyles, ...(style ?? {}) }}
            >
              {children}
            </div>
          );
        },
      ),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [gridTemplateColumns, gapPx],
  );

  return (
    <Box position="relative" pb={{ xs: 1, sm: 2 }}>
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
        <VirtuosoGrid
          useWindowScroll
          totalCount={tiles.length}
          overscan={600}
          components={{
            List: listComponent,
          }}
          itemContent={renderTile}
        />
      ) : (
        <Box sx={gridStyles}>
          {tiles.map((tile: FileSystemTile) => (
            <TileItem
              key={tile.kind === "folder" ? tile.node.id : tile.file.id}
              tile={tile}
              folderOperations={folderOperations}
              fileOperations={fileOperations}
              fileNamePlaceholder={fileNamePlaceholder}
            />
          ))}
        </Box>
      )}
    </Box>
  );
};
