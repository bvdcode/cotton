import React from "react";
import { Box, Checkbox } from "@mui/material";
import { Folder, Download, Edit, Delete, Share } from "@mui/icons-material";
import { FolderCard } from "../FolderCard";
import { RenamableItemCard } from "../RenamableItemCard";
import { InlineRenameField } from "../InlineRenameField";
import { getFileIcon } from "../../utils/icons";
import { formatBytes } from "../../../../shared/utils/formatBytes";
import {
  getFileTypeInfo,
} from "../../utils/fileTypes";
import type {
  FileSystemTile,
  FolderOperations,
  FileOperations,
} from "../../types/FileListViewTypes";
import { alpha, useTheme } from "@mui/material/styles";
import { useTranslation } from "react-i18next";

interface BlurredPreviewImageProps {
  previewUrl: string;
  alt: string;
  blurOpacity: number;
  cursor: React.CSSProperties["cursor"];
  shouldLightenBackdrop: boolean;
  invertInDark: boolean;
}

const BlurredPreviewImage: React.FC<BlurredPreviewImageProps> = ({
  previewUrl,
  alt,
  blurOpacity,
  cursor,
  shouldLightenBackdrop,
  invertInDark,
}) => {
  const [imageFit, setImageFit] = React.useState<"contain" | "cover">(
    "contain",
  );

  const handleLoad = React.useCallback(
    (e: React.SyntheticEvent<HTMLImageElement>) => {
      const img = e.currentTarget;
      const nextFit =
        img.naturalWidth > img.naturalHeight ? "cover" : "contain";
      setImageFit((prev) => (prev === nextFit ? prev : nextFit));
    },
    [],
  );

  return (
    <Box
      sx={{
        width: "100%",
        height: "100%",
        position: "relative",
      }}
    >
      <Box
        component="img"
        src={previewUrl}
        alt=""
        aria-hidden
        sx={{
          position: "absolute",
          inset: 0,
          display: "block",
          width: "100%",
          height: "100%",
          objectFit: "cover",
          filter: "blur(24px)",
          transform: "scale(1.15)",
          opacity: blurOpacity,
        }}
      />
      <Box
        component="img"
        src={previewUrl}
        alt={alt}
        loading="lazy"
        decoding="async"
        onLoad={handleLoad}
        sx={(theme) => ({
          position: "relative",
          display: "block",
          width: "100%",
          height: "100%",
          objectFit: imageFit,
          cursor,
          ...(shouldLightenBackdrop && {
            backgroundColor: alpha(theme.palette.common.white, 0.75),
          }),
          ...(invertInDark &&
            theme.palette.mode === "dark" && {
              filter: "invert(1)",
            }),
        })}
      />
    </Box>
  );
};

interface NewFolderCardProps {
  newFolderName: string;
  onNewFolderNameChange: (name: string) => void;
  onConfirmNewFolder: () => Promise<void>;
  onCancelNewFolder: () => void;
  folderNamePlaceholder: string;
}

export const NewFolderCard: React.FC<NewFolderCardProps> = ({
  newFolderName,
  onNewFolderNameChange,
  onConfirmNewFolder,
  onCancelNewFolder,
  folderNamePlaceholder,
}) => (
  <Box
    sx={{
      border: "2px solid",
      borderColor: "primary.main",
      borderRadius: 1,
      aspectRatio: "1 / 1",
      display: "flex",
      flexDirection: "column",
      p: { xs: 1, sm: 1.25, md: 1 },
      bgcolor: "action.hover",
    }}
  >
    <Box
      sx={{
        width: "100%",
        flex: 1,
        minHeight: 0,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        borderRadius: 1.5,
        overflow: "hidden",
        "& > svg": { width: "70%", height: "70%" },
      }}
    >
      <Folder sx={{ color: "primary.main" }} />
    </Box>
    <Box display="flex" alignItems="center" gap={0.5}>
      <InlineRenameField
        value={newFolderName}
        onChange={onNewFolderNameChange}
        onConfirm={onConfirmNewFolder}
        onCancel={onCancelNewFolder}
        placeholder={folderNamePlaceholder}
      />
    </Box>
  </Box>
);

interface TileItemProps {
  tile: FileSystemTile;
  folderOperations: FolderOperations;
  fileOperations: FileOperations;
  fileNamePlaceholder: string;
  readOnly?: boolean;
  selectionMode?: boolean;
  selected?: boolean;
  onToggle?: (shiftKey: boolean) => void;
}

/**
 * Renders a single tile (folder or file) in the grid.
 * Extracted for reuse by both plain and virtualized grid.
 */
export const TileItem: React.FC<TileItemProps> = React.memo(
  ({ tile, folderOperations, fileOperations, fileNamePlaceholder, readOnly = false, selectionMode = false, selected = false, onToggle }) => {
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
      const folderContent = (
        <FolderCard
          folder={tile.node}
          isRenaming={folderOperations.isRenaming(tile.node.id)}
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
          onContextMenu={(e) => {
            e.preventDefault();
          }}
          onPointerDownCapture={handlePointerDownCapture}
          onPointerMoveCapture={handlePointerMoveCapture}
          onPointerUpCapture={handlePointerUpCapture}
          onPointerCancelCapture={handlePointerCancelCapture}
          onClickCapture={handleClickCapture}
        >
          {checkbox}
          {folderContent}
        </Box>
      );
    }

    const typeInfo = getFileTypeInfo(tile.file.name, tile.file.contentType);
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

    const fileContent = (
      <RenamableItemCard
        variant="squareTile"
        icon={icon}
        title={tile.file.name}
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
          ...(!readOnly && fileOperations.onShare
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
        isRenaming={fileOperations.isRenaming(tile.file.id)}
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
        onContextMenu={(e) => {
          e.preventDefault();
        }}
        onPointerDownCapture={handlePointerDownCapture}
        onPointerMoveCapture={handlePointerMoveCapture}
        onPointerUpCapture={handlePointerUpCapture}
        onPointerCancelCapture={handlePointerCancelCapture}
        onClickCapture={handleClickCapture}
      >
        {checkbox}
        {fileContent}
      </Box>
    );
  },
);
