import React from "react";
import { Box, Checkbox, TextField } from "@mui/material";
import { Folder, Download, Edit, Delete, Share } from "@mui/icons-material";
import { FolderCard } from "../FolderCard";
import { RenamableItemCard } from "../RenamableItemCard";
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
          onStartRename={() =>
            folderOperations.onStartRename(tile.node.id, tile.node.name)
          }
          onDelete={() =>
            folderOperations.onDelete(tile.node.id, tile.node.name)
          }
          onShare={
            folderOperations.onShare
              ? () => folderOperations.onShare?.(tile.node.id, tile.node.name)
              : undefined
          }
          onClick={(e) =>
            selectionMode
              ? onToggle?.(!!(e as React.MouseEvent).shiftKey)
              : folderOperations.onClick(tile.node.id)
          }
          variant="squareTile"
          readOnly={readOnly}
        />
      );

      return (
        <Box position="relative">
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
      if (selectionMode) {
        onToggle?.(!!(e as React.MouseEvent | undefined)?.shiftKey);
        return;
      }

      if (isImage || isVideo) {
        fileOperations.onMediaClick?.(tile.file.id);
        return;
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
          {
            icon: <Download />,
            onClick: () =>
              fileOperations.onDownload(tile.file.id, tile.file.name),
            tooltip: t("actions.download", { ns: "common" }),
          },
          ...(readOnly
            ? []
            : [
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
              ]),
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

    return (
      <Box position="relative">
        {checkbox}
        {fileContent}
      </Box>
    );
  },
);
