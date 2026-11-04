import React, { useCallback, useEffect } from "react";
import { Box, Typography } from "@mui/material";
import FolderOpenOutlinedIcon from "@mui/icons-material/FolderOpenOutlined";
import { Virtuoso } from "react-virtuoso";
import type {
  IFileListView,
  FileSystemTile,
} from "@shared/types/FileListViewTypes";
import Loader from "../../../../shared/ui/Loader";
import { NewFolderCard } from "./NewFolderCard";
import { TileItem } from "./TileItem";
import { useTileDragAndDrop } from "./hooks/useTileDragAndDrop";
import { useTileGridLayout } from "./hooks/useTileGridLayout";
import { useTilesKeyboard } from "./useTilesKeyboard";
import { useTileMovement } from "./hooks/useTileMovement";

const VIRTUALIZATION_THRESHOLD = 80;

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
  onLoadMore,
  tileSize = "medium",
  selectionMode = false,
  selectedIds,
  onToggleItem,
  moveSupport,
}) => {
  const { containerRef, scrollParent, columns, gapPx, gridStyles } =
    useTileGridLayout(tileSize);
  const {
    cutItemIds,
    dropTargetId,
    handleMoveDragStart,
    handleMoveDragOver,
    handleMoveDragLeave,
    handleMoveDrop,
  } = useTileDragAndDrop({
    tiles,
    moveSupport,
    selectedIds,
    selectionMode,
  });

  const shouldVirtualize = tiles.length > VIRTUALIZATION_THRESHOLD;
  const leadingItemCount = isCreatingFolder ? 1 : 0;

  const {
    virtuosoRef,
    orderedIds,
    handleFocusCapture,
    handlePointerDownCapture,
    handleKeyDownCapture,
  } = useTilesKeyboard({
    tiles,
    selectedIds,
    columns,
    leadingItemCount,
    containerRef,
    shouldVirtualize,
    folderOperations,
    fileOperations,
    readOnly,
    onNavigateBack,
  });

  useTileMovement(containerRef, orderedIds, leadingItemCount);

  useEffect(() => {
    if (isCreatingFolder) {
      virtuosoRef.current?.scrollToIndex({ index: 0, align: "start" });
    }
  }, [isCreatingFolder, virtuosoRef]);

  const renderTile = useCallback(
    (tile: FileSystemTile, index: number) => {
      const tileId = tile.kind === "folder" ? tile.node.id : tile.file.id;
      const dimmed = cutItemIds?.has(tileId) ?? false;
      const isFolder = tile.kind === "folder";

      return (
        <Box key={tileId} data-tile-index={index} data-tile-id={tileId}>
          <TileItem
            tile={tile}
            folderOperations={folderOperations}
            fileOperations={fileOperations}
            readOnly={readOnly}
            fileNamePlaceholder={fileNamePlaceholder}
            tileSize={tileSize}
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
            dimmed={dimmed}
            draggable={!!moveSupport && !readOnly}
            onMoveDragStart={
              moveSupport ? (e) => handleMoveDragStart(tileId, e) : undefined
            }
            onMoveDragOver={
              moveSupport && isFolder
                ? (e) => handleMoveDragOver(tileId, e)
                : undefined
            }
            onMoveDragLeave={
              moveSupport && isFolder
                ? (e) => handleMoveDragLeave(tileId, e)
                : undefined
            }
            onMoveDrop={
              moveSupport && isFolder
                ? (e) => handleMoveDrop(tileId, e)
                : undefined
            }
            dropActive={isFolder && dropTargetId === tileId}
          />
        </Box>
      );
    },
    [
      cutItemIds,
      dropTargetId,
      fileNamePlaceholder,
      fileOperations,
      folderOperations,
      handleMoveDragLeave,
      handleMoveDragOver,
      handleMoveDragStart,
      handleMoveDrop,
      moveSupport,
      onToggleItem,
      orderedIds,
      readOnly,
      selectedIds,
      selectionMode,
      tileSize,
    ],
  );

  const newFolderCard = isCreatingFolder && (
    <NewFolderCard
      newFolderName={newFolderName}
      onNewFolderNameChange={onNewFolderNameChange}
      onConfirmNewFolder={onConfirmNewFolder}
      onCancelNewFolder={onCancelNewFolder}
      folderNamePlaceholder={folderNamePlaceholder}
    />
  );

  const showLoading = loading && tiles.length === 0;
  const showEmpty =
    !loading &&
    !isCreatingFolder &&
    tiles.length === 0 &&
    Boolean(emptyStateText);

  return (
    <Box
      ref={containerRef}
      onPointerDownCapture={handlePointerDownCapture}
      onFocusCapture={handleFocusCapture}
      onKeyDownCapture={handleKeyDownCapture}
      sx={{
        position: "relative",
        pb: { xs: 1, sm: 2 },
        ...((showLoading || showEmpty) && {
          display: "flex",
          flexDirection: "column",
          flex: 1,
          minHeight: 0,
        }),
      }}
    >
      {showLoading && <Loader title={loadingTitle} caption={loadingCaption} />}
      {showEmpty && (
        <Box
          sx={{
            flex: 1,
            minHeight: 0,
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            justifyContent: "center",
            textAlign: "center",
            gap: 1.5,
            color: "text.secondary",
          }}
        >
          <FolderOpenOutlinedIcon sx={{ fontSize: 56, opacity: 0.4 }} />
          <Typography
            variant="h6"
            sx={{
              color: "text.secondary",
              fontWeight: 500,
            }}
          >
            {emptyStateText}
          </Typography>
        </Box>
      )}

      {!showLoading &&
        !showEmpty &&
        (shouldVirtualize ? (
          <Virtuoso
            ref={virtuosoRef}
            customScrollParent={scrollParent ?? undefined}
            totalCount={Math.ceil((tiles.length + leadingItemCount) / columns)}
            overscan={600}
            itemContent={(rowIndex: number) => {
              const start = rowIndex * columns - leadingItemCount;
              const firstTileIndex = Math.max(0, start);
              const rowTiles = tiles.slice(firstTileIndex, start + columns);

              return (
                <Box
                  sx={{
                    display: "grid",
                    gap: `${gapPx}px`,
                    gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))`,
                    pb: `${gapPx}px`,
                  }}
                >
                  {rowIndex === 0 && newFolderCard}
                  {rowTiles.map((tile: FileSystemTile, tileOffset: number) =>
                    renderTile(tile, firstTileIndex + tileOffset),
                  )}
                </Box>
              );
            }}
            endReached={onLoadMore}
          />
        ) : (
          <Box sx={gridStyles}>
            {newFolderCard}
            {tiles.map((tile: FileSystemTile, index: number) =>
              renderTile(tile, index),
            )}
          </Box>
        ))}
    </Box>
  );
};
