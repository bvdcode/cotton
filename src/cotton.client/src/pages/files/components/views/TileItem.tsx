import React from "react";
import { Box, Checkbox, TextField } from "@mui/material";
import { Folder, Download, Edit, Delete, Share } from "@mui/icons-material";
import { FolderCard } from "../FolderCard";
import { RenamableItemCard } from "../RenamableItemCard";
import { getFileIcon } from "../../utils/icons";
import { formatBytes } from "../../../../shared/utils/formatBytes";
import {
  isImageFile,
  isPdfFile,
  isTextFile,
  isVideoFile,
} from "../../utils/fileTypes";
import type {
  FileSystemTile,
  FolderOperations,
  FileOperations,
} from "../../types/FileListViewTypes";
import { alpha, useTheme } from "@mui/material/styles";
import { useTranslation } from "react-i18next";

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
      <TextField
        autoFocus
        fullWidth
        size="small"
        value={newFolderName}
        onChange={(e) => onNewFolderNameChange(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === "Enter") void onConfirmNewFolder();
          else if (e.key === "Escape") onCancelNewFolder();
        }}
        onBlur={onConfirmNewFolder}
        placeholder={folderNamePlaceholder}
        slotProps={{
          input: { sx: { fontSize: { xs: "0.8rem", md: "0.85rem" } } },
        }}
      />
    </Box>
  </Box>
);

interface TileItemProps {
  tile: FileSystemTile;
  folderOperations: FolderOperations;
  fileOperations: FileOperations;
  fileNamePlaceholder: string;
  selectionMode?: boolean;
  selected?: boolean;
  onToggle?: () => void;
}

/**
 * Renders a single tile (folder or file) in the grid.
 * Extracted for reuse by both plain and virtualized grid.
 */
export const TileItem: React.FC<TileItemProps> = React.memo(
  ({ tile, folderOperations, fileOperations, fileNamePlaceholder, selectionMode = false, selected = false, onToggle }) => {
    const theme = useTheme();
    const isDarkMode = theme.palette.mode === "dark";
    const { t } = useTranslation(["common"]);

    if (tile.kind === "folder") {
      const folderContent = (
        <FolderCard
          folder={tile.node}
          isRenaming={folderOperations.isRenaming(tile.node.id)}
          renamingName={folderOperations.getRenamingName()}
          onRenamingNameChange={folderOperations.onRenamingNameChange}
          onConfirmRename={folderOperations.onConfirmRename}
          onCancelRename={folderOperations.onCancelRename}
          onStartRename={() =>
            folderOperations.onStartRename(tile.node.id, tile.node.name)
          }
          onDelete={() =>
            folderOperations.onDelete(tile.node.id, tile.node.name)
          }
          onClick={selectionMode ? () => onToggle?.() : () => folderOperations.onClick(tile.node.id)}
          variant="squareTile"
        />
      );

      if (selectionMode) {
        return (
          <Box position="relative">
            <Checkbox
              checked={selected}
              onChange={() => onToggle?.()}
              sx={{
                position: "absolute",
                top: 4,
                left: 4,
                zIndex: 5,
                bgcolor: "background.paper",
                borderRadius: 1,
                p: 0.25,
              }}
              size="small"
            />
            {folderContent}
          </Box>
        );
      }

      return folderContent;
    }

    const isImage = isImageFile(tile.file.name);
    const isVideo = isVideoFile(tile.file.name);
    const shouldLightenPreviewBackdrop =
      isDarkMode &&
      (isPdfFile(tile.file.name) || isTextFile(tile.file.name));
    const preview = getFileIcon(
      tile.file.encryptedFilePreviewHashHex ?? null,
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
      if (previewUrl && (isImage || isVideo)) {
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
                opacity: 0.6,
              }}
            />
            <Box
              component="img"
              src={previewUrl}
              alt={tile.file.name}
              loading="lazy"
              decoding="async"
              sx={{
                position: "relative",
                display: "block",
                width: "100%",
                height: "100%",
                objectFit: "contain",
                cursor: isImage || isVideo ? "pointer" : "default",
              }}
            />
          </Box>
        );
      }
      if (previewUrl) {
        const isTextPreview = isTextFile(tile.file.name);
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
                opacity: 0.5,
              }}
            />
            <Box
              component="img"
              src={previewUrl}
              alt={tile.file.name}
              loading="lazy"
              decoding="async"
              sx={(t) => ({
                position: "relative",
                display: "block",
                width: "100%",
                height: "100%",
                objectFit: "contain",
                ...(shouldLightenPreviewBackdrop && {
                  backgroundColor: alpha(t.palette.common.white, 0.75),
                }),
                ...(isTextPreview &&
                  t.palette.mode === "dark" && {
                    filter: "invert(1)",
                  }),
              })}
            />
          </Box>
        );
      }
      return preview;
    })();

    const fileClick = selectionMode
      ? () => onToggle?.()
      : isImage || isVideo
        ? () => fileOperations.onMediaClick?.(tile.file.id)
        : () =>
            fileOperations.onClick(
              tile.file.id,
              tile.file.name,
              tile.file.sizeBytes,
            );

    const fileContent = (
      <RenamableItemCard
        variant="squareTile"
        icon={icon}
        title={tile.file.name}
        subtitle={formatBytes(tile.file.sizeBytes)}
        onClick={fileClick}
        iconContainerSx={iconContainerSx}
        actions={[
          {
            icon: <Download />,
            onClick: () =>
              fileOperations.onDownload(tile.file.id, tile.file.name),
            tooltip: t("actions.download", { ns: "common" }),
          },
          {
            icon: <Share />,
            onClick: () =>
              fileOperations.onShare(tile.file.id, tile.file.name),
            tooltip: t("actions.share", { ns: "common" }),
          },
          {
            icon: <Edit />,
            onClick: () =>
              fileOperations.onStartRename(tile.file.id, tile.file.name),
            tooltip: t("actions.rename", { ns: "common" }),
          },
          {
            icon: <Delete />,
            onClick: () =>
              fileOperations.onDelete(tile.file.id, tile.file.name),
            tooltip: t("actions.delete", { ns: "common" }),
          },
        ]}
        isRenaming={fileOperations.isRenaming(tile.file.id)}
        renamingValue={fileOperations.getRenamingName()}
        onRenamingValueChange={fileOperations.onRenamingNameChange}
        onConfirmRename={() => {
          void fileOperations.onConfirmRename();
        }}
        onCancelRename={fileOperations.onCancelRename}
        placeholder={fileNamePlaceholder}
      />
    );

    if (selectionMode) {
      return (
        <Box position="relative">
          <Checkbox
            checked={selected}
            onChange={() => onToggle?.()}
            sx={{
              position: "absolute",
              top: 4,
              left: 4,
              zIndex: 5,
              bgcolor: "background.paper",
              borderRadius: 1,
              p: 0.25,
            }}
            size="small"
          />
          {fileContent}
        </Box>
      );
    }

    return fileContent;
  },
);
