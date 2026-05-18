import React from "react";
import { Box, Checkbox } from "@mui/material";
import {
  ContentCut,
  Delete,
  Download,
  Edit,
  LockOutlined,
  Restore,
  Share,
} from "@mui/icons-material";
import { FolderCard } from "../FolderCard";
import { RenamableItemCard } from "../RenamableItemCard";
import { getFileIcon } from "@shared/utils/icons";
import { formatBytes } from "../../../../shared/utils/formatBytes";
import { getFileTypeInfo } from "@shared/utils/fileTypes";
import type {
  FileSystemTile,
  FolderOperations,
  FileOperations,
  TilesSize,
} from "@shared/types/FileListViewTypes";
import { alpha, useTheme } from "@mui/material/styles";
import { useTranslation } from "react-i18next";
import { BlurredPreviewImage } from "./BlurredPreviewImage";
import {
  isFileEncrypted,
  isFolderEncryptionPolicyEnabled,
} from "../../../../shared/crypto";

interface TileItemProps {
  tile: FileSystemTile;
  folderOperations: FolderOperations;
  fileOperations: FileOperations;
  fileNamePlaceholder: string;
  tileSize?: TilesSize;
  readOnly?: boolean;
  selectionMode?: boolean;
  selected?: boolean;
  onToggle?: (shiftKey: boolean) => void;
  /** Renders the tile semi-transparent to indicate it is in the cut buffer. */
  dimmed?: boolean;
  /** Whether this tile can initiate a move drag. */
  draggable?: boolean;
  onMoveDragStart?: (event: React.DragEvent<HTMLDivElement>) => void;
  /** Drop handlers (active only for folder targets). */
  onMoveDragOver?: (event: React.DragEvent<HTMLDivElement>) => void;
  onMoveDragLeave?: (event: React.DragEvent<HTMLDivElement>) => void;
  onMoveDrop?: (event: React.DragEvent<HTMLDivElement>) => void;
  /** When true, highlights the tile as the active drop target. */
  dropActive?: boolean;
}

/**
 * Renders a single tile (folder or file) in the grid.
 * Extracted for reuse by both plain and virtualized grid.
 */
export const TileItem: React.FC<TileItemProps> = React.memo(
  ({
    tile,
    folderOperations,
    fileOperations,
    fileNamePlaceholder,
    tileSize = "medium",
    readOnly = false,
    selectionMode = false,
    selected = false,
    onToggle,
    dimmed = false,
    draggable = false,
    onMoveDragStart,
    onMoveDragOver,
    onMoveDragLeave,
    onMoveDrop,
    dropActive = false,
  }) => {
    const theme = useTheme();
    const isDarkMode = theme.palette.mode === "dark";
    const { t } = useTranslation(["common"]);

    const longPressTimerRef = React.useRef<number | null>(null);
    const suppressClickUntilRef = React.useRef(0);
    const longPressStartRef = React.useRef<{ x: number; y: number } | null>(null);

    const clearLongPress = React.useCallback(() => {
      if (longPressTimerRef.current !== null) {
        window.clearTimeout(longPressTimerRef.current);
        longPressTimerRef.current = null;
      }
      longPressStartRef.current = null;
    }, []);

    const shouldIgnoreLongPressTarget = React.useCallback((target: EventTarget | null) => {
      if (!(target instanceof Element)) return false;
      return Boolean(
        target.closest(
          ".card-menu-slot, .card-menu-button, button, a, input, textarea, [role='menuitem']",
        ),
      );
    }, []);

    const handlePointerDownCapture = React.useCallback(
      (e: React.PointerEvent) => {
        if (!onToggle) return;
        if (selectionMode) return;
        if (readOnly) return;
        if (e.button !== 0) return;
        if (e.shiftKey) return;
        if (shouldIgnoreLongPressTarget(e.target)) return;

        longPressStartRef.current = { x: e.clientX, y: e.clientY };

        clearLongPress();
        longPressTimerRef.current = window.setTimeout(() => {
          suppressClickUntilRef.current = Date.now() + 450;
          onToggle(false);
        }, 450);
      },
      [clearLongPress, onToggle, readOnly, selectionMode, shouldIgnoreLongPressTarget],
    );

    const handlePointerMoveCapture = React.useCallback(
      (e: React.PointerEvent) => {
        if (longPressTimerRef.current === null) return;
        const start = longPressStartRef.current;
        if (!start) return;
        const dx = Math.abs(e.clientX - start.x);
        const dy = Math.abs(e.clientY - start.y);
        if (dx > 8 || dy > 8) {
          clearLongPress();
        }
      },
      [clearLongPress],
    );

    const handlePointerUpCapture = React.useCallback(() => {
      clearLongPress();
    }, [clearLongPress]);

    const handlePointerCancelCapture = React.useCallback(() => {
      clearLongPress();
    }, [clearLongPress]);

    const handleClickCapture = React.useCallback((e: React.MouseEvent) => {
      if (Date.now() > suppressClickUntilRef.current) return;
      e.preventDefault();
      e.stopPropagation();
    }, []);

    const checkbox = (
      <Checkbox
        checked={selected}
        onChange={(e) => {
          const nativeEvent = e.nativeEvent as MouseEvent;
          onToggle?.(!!nativeEvent.shiftKey);
        }}
        sx={{
          position: "absolute",
          top: 4,
          left: 4,
          zIndex: 5,
          display: selectionMode ? "inline-flex" : "none",
        }}
        size="small"
      />
    );

    if (tile.kind === "folder") {
      const isRenamingFolder = folderOperations.isRenaming(tile.node.id);
      const folderEncryptionPolicy =
        folderOperations.getEncryptionPolicyState?.(tile.node);
      const folderContent = (
        <FolderCard
          folder={tile.node}
          encryptionPolicy={folderEncryptionPolicy}
          isRenaming={isRenamingFolder}
          renamingName={folderOperations.getRenamingName()}
          onRenamingNameChange={folderOperations.onRenamingNameChange}
          onConfirmRename={folderOperations.onConfirmRename}
          onCancelRename={folderOperations.onCancelRename}
          onStartRename={
            folderOperations.onStartRename
              ? () => folderOperations.onStartRename?.(tile.node.id, tile.node.name)
              : undefined
          }
          onDelete={
            folderOperations.onDelete
              ? () => folderOperations.onDelete?.(tile.node.id, tile.node.name)
              : undefined
          }
          onShare={
            folderOperations.onShare
              ? () => folderOperations.onShare?.(tile.node.id, tile.node.name)
              : undefined
          }
          onCut={
            folderOperations.onCut
              ? () => folderOperations.onCut?.(tile.node.id)
              : undefined
          }
          onToggleEncryptionPolicy={
            folderOperations.onToggleEncryptionPolicy
              ? () =>
                  folderOperations.onToggleEncryptionPolicy?.(
                    tile.node.id,
                    folderEncryptionPolicy?.explicitEnabled ??
                      isFolderEncryptionPolicyEnabled(tile.node.metadata),
                  )
              : undefined
          }
          onRestore={
            folderOperations.onRestore
              ? () => folderOperations.onRestore?.(tile.node.id, tile.node.name)
              : undefined
          }
          onClick={(e) => {
            const shiftKey = !!(e as React.MouseEvent).shiftKey;

            if (shiftKey && onToggle) {
              onToggle(true);
              return;
            }

            if (selectionMode) {
              onToggle?.(shiftKey);
              return;
            }

            folderOperations.onClick(tile.node.id);
          }}
          variant="squareTile"
          readOnly={readOnly}
        />
      );

      return (
        <Box
          position="relative"
          draggable={draggable && !isRenamingFolder}
          onDragStart={onMoveDragStart}
          onDragOver={onMoveDragOver}
          onDragLeave={onMoveDragLeave}
          onDrop={onMoveDrop}
          onContextMenu={(e) => {
            e.preventDefault();
          }}
          onPointerDownCapture={handlePointerDownCapture}
          onPointerMoveCapture={handlePointerMoveCapture}
          onPointerUpCapture={handlePointerUpCapture}
          onPointerCancelCapture={handlePointerCancelCapture}
          onClickCapture={handleClickCapture}
          sx={{
            opacity: dimmed ? 0.45 : 1,
            transition: "opacity 120ms ease-out, box-shadow 120ms ease-out",
            ...(dropActive && {
              outline: "2px solid",
              outlineColor: "primary.main",
              outlineOffset: 1,
              borderRadius: 1,
            }),
          }}
        >
          {checkbox}
          {folderContent}
        </Box>
      );
    }

    const typeInfo = getFileTypeInfo(tile.file.name, tile.file.contentType, {
      requiresVideoTranscoding: tile.file.requiresVideoTranscoding ?? false,
    });
    const isImage = typeInfo.type === "image";
    const isVideo = typeInfo.type === "video";
    const isPdf = typeInfo.type === "pdf";
    const isText = typeInfo.type === "text";
    const shouldLightenPreviewBackdrop =
      isDarkMode &&
      (isPdf || isText);
    const preview = getFileIcon(
      tile.file.previewHashEncryptedHex ?? null,
      tile.file.name,
      tile.file.contentType,
      {
        extensionLabelMaxLength: 4,
        hideLongExtensionLabel: true,
        hideInvalidExtensionLabel: true,
        hideExtensionLabel: tileSize === "small",
      },
    );
    const previewUrl = typeof preview === "string" ? preview : null;

    const iconContainerSx = previewUrl
      ? {
          ...(shouldLightenPreviewBackdrop && {
            bgcolor: alpha(theme.palette.common.white, 0.75),
          }),
        }
      : undefined;

    const icon = (() => {
      if (previewUrl) {
        const isTextPreview = isText;
        const cursor: React.CSSProperties["cursor"] = "inherit";
        return (
          <BlurredPreviewImage
            previewUrl={previewUrl}
            alt={tile.file.name}
            blurOpacity={isImage || isVideo ? 0.6 : 0.5}
            cursor={cursor}
            shouldLightenBackdrop={shouldLightenPreviewBackdrop}
            invertInDark={isTextPreview}
          />
        );
      }
      return preview;
    })();

    const fileClick = (e?: React.SyntheticEvent) => {
      const shiftKey = !!(e as React.MouseEvent | undefined)?.shiftKey;

      if (shiftKey && onToggle) {
        onToggle(true);
        return;
      }

      if (selectionMode) {
        onToggle?.(shiftKey);
        return;
      }

      if (isImage || isVideo) {
        if (fileOperations.onMediaClick) {
          fileOperations.onMediaClick(tile.file.id);
          return;
        }
      }

      fileOperations.onClick(tile.file.id, tile.file.name, tile.file.sizeBytes);
    };

    const fileEncrypted = isFileEncrypted(
      "metadata" in tile.file ? tile.file.metadata : undefined,
    );

    const isRenamingFile = fileOperations.isRenaming(tile.file.id);
    const fileContent = (
      <RenamableItemCard
        variant="squareTile"
        icon={icon}
        title={tile.file.name}
        cornerAdornment={
          fileEncrypted ? (
            <LockOutlined
              fontSize="small"
              titleAccess={t("clientEncryption.fileEncryptedHint", {
                ns: "common",
              })}
            />
          ) : undefined
        }
        subtitle={formatBytes(tile.file.sizeBytes)}
        onClick={fileClick}
        iconContainerSx={iconContainerSx}
        actions={[
          ...(fileOperations.onDownload
            ? [
                {
                  icon: <Download />,
                  onClick: () =>
                    fileOperations.onDownload?.(tile.file.id, tile.file.name),
                  tooltip: t("actions.download", { ns: "common" }),
                },
              ]
            : []),
          ...(!readOnly && fileOperations.onShare && !fileEncrypted
            ? [
                {
                  icon: <Share />,
                  onClick: () =>
                    fileOperations.onShare?.(tile.file.id, tile.file.name),
                  tooltip: t("actions.share", { ns: "common" }),
                },
              ]
            : []),
          ...(!readOnly && fileOperations.onStartRename
            ? [
                {
                  icon: <Edit />,
                  onClick: () =>
                    fileOperations.onStartRename?.(tile.file.id, tile.file.name),
                  tooltip: t("actions.rename", { ns: "common" }),
                },
              ]
            : []),
          ...(!readOnly && fileOperations.onCut
            ? [
                {
                  icon: <ContentCut />,
                  onClick: () => fileOperations.onCut?.(tile.file.id),
                  tooltip: t("files:move.cut"),
                },
              ]
            : []),
          ...(!readOnly && fileOperations.onRestore
            ? [
                {
                  icon: <Restore />,
                  onClick: () =>
                    fileOperations.onRestore?.(tile.file.id, tile.file.name),
                  tooltip: t("actions.restore", { ns: "common" }),
                },
              ]
            : []),
          ...(!readOnly && fileOperations.onDelete
            ? [
                {
                  icon: <Delete />,
                  onClick: () =>
                    fileOperations.onDelete?.(tile.file.id, tile.file.name),
                  tooltip: t("actions.delete", { ns: "common" }),
                },
              ]
            : []),
        ]}
        isRenaming={isRenamingFile}
        renamingValue={fileOperations.getRenamingName()}
        onRenamingValueChange={fileOperations.onRenamingNameChange}
        onConfirmRename={() => {
          void fileOperations.onConfirmRename?.();
        }}
        onCancelRename={fileOperations.onCancelRename ?? (() => {})}
        placeholder={fileNamePlaceholder}
      />
    );

    return (
      <Box
        position="relative"
        draggable={draggable && !isRenamingFile}
        onDragStart={onMoveDragStart}
        onContextMenu={(e) => {
          e.preventDefault();
        }}
        onPointerDownCapture={handlePointerDownCapture}
        onPointerMoveCapture={handlePointerMoveCapture}
        onPointerUpCapture={handlePointerUpCapture}
        onPointerCancelCapture={handlePointerCancelCapture}
        onClickCapture={handleClickCapture}
        sx={{
          opacity: dimmed ? 0.45 : 1,
          transition: "opacity 120ms ease-out",
        }}
      >
        {checkbox}
        {fileContent}
      </Box>
    );
  },
);
