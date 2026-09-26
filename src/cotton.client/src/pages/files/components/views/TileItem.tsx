import React from "react";
import {
  ContentCut,
  Delete,
  Download,
  Edit,
  History,
  LockOutlined,
  Restore,
  Share,
} from "@mui/icons-material";
import { alpha, useTheme } from "@mui/material/styles";
import { useTranslation } from "react-i18next";
import { RenamableItemCard } from "../RenamableItemCard";
import { getFileIcon } from "@shared/utils/icons";
import { formatBytes } from "../../../../shared/utils/formatBytes";
import { getFileTypeInfo } from "@shared/utils/fileTypes";
import type {
  FileOperations,
  FileSystemTile,
  FolderOperations,
  TilesSize,
} from "@shared/types/FileListViewTypes";
import { BlurredPreviewImage } from "./BlurredPreviewImage";
import { isFileEncrypted } from "../../../../shared/crypto";

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

type FileTile = Extract<FileSystemTile, { kind: "file" }>;
type RenamableItemCardActions = NonNullable<
  React.ComponentProps<typeof RenamableItemCard>["actions"]
>;

import { SelectionCheckbox, TileFrame } from "./TileSelectionFrame";
import {
  useLongPressSelection,
  getShiftKey,
  type TileLongPressHandlers,
} from "./tileSelection";
import { FolderTileItem } from "./FolderTileItem";

const FilePreviewIcon = ({
  file,
  tileSize,
}: {
  file: FileTile["file"];
  tileSize: TilesSize;
}): React.ReactNode => {
  const theme = useTheme();
  const typeInfo = getFileTypeInfo(file.name, file.contentType, {
    requiresVideoTranscoding: file.requiresVideoTranscoding ?? false,
  });
  const isDarkMode = theme.palette.mode === "dark";
  const isPdfOrText = typeInfo.type === "pdf" || typeInfo.type === "text";
  const shouldLightenPreviewBackdrop = isDarkMode && isPdfOrText;
  const preview = getFileIcon(
    file.previewHashEncryptedHex ?? null,
    file.name,
    file.contentType,
    {
      extensionLabelMaxLength: 3,
      hideLongExtensionLabel: true,
      hideInvalidExtensionLabel: true,
      hideExtensionLabel: tileSize === "small",
    },
  );
  const previewUrl = typeof preview === "string" ? preview : null;

  if (!previewUrl) {
    return preview;
  }

  return (
    <BlurredPreviewImage
      previewUrl={previewUrl}
      alt={file.name}
      blurOpacity={
        typeInfo.type === "image" || typeInfo.type === "video" ? 0.6 : 0.5
      }
      cursor="inherit"
      shouldLightenBackdrop={shouldLightenPreviewBackdrop}
      invertInDark={typeInfo.type === "text"}
    />
  );
};

const useFileIconContainerSx = (
  file: FileTile["file"],
): React.ComponentProps<typeof RenamableItemCard>["iconContainerSx"] => {
  const theme = useTheme();
  const isDarkMode = theme.palette.mode === "dark";
  const typeInfo = getFileTypeInfo(file.name, file.contentType, {
    requiresVideoTranscoding: file.requiresVideoTranscoding ?? false,
  });
  const hasPreviewUrl =
    typeof getFileIcon(
      file.previewHashEncryptedHex ?? null,
      file.name,
      file.contentType,
    ) === "string";

  if (!hasPreviewUrl || !isDarkMode) {
    return undefined;
  }

  return typeInfo.type === "pdf" || typeInfo.type === "text"
    ? { bgcolor: alpha(theme.palette.common.white, 0.75) }
    : undefined;
};

const buildFileTileActions = (options: {
  file: FileTile["file"];
  fileEncrypted: boolean;
  fileOperations: FileOperations;
  readOnly: boolean;
  t: ReturnType<typeof useTranslation<["common"]>>["t"];
}): RenamableItemCardActions => {
  const { file, fileEncrypted, fileOperations, readOnly, t } = options;
  const actions: RenamableItemCardActions = [];

  if (fileOperations.onDownload) {
    actions.push({
      icon: <Download />,
      onClick: () => fileOperations.onDownload?.(file.id, file.name),
      tooltip: t("actions.download", { ns: "common" }),
    });
  }
  if (fileOperations.onVersions) {
    actions.push({
      icon: <History />,
      onClick: () => fileOperations.onVersions?.(file.id, file.name),
      tooltip: t("common:actions.versions"),
    });
  }
  if (!readOnly && fileOperations.onShare && !fileEncrypted) {
    actions.push({
      icon: <Share />,
      onClick: () => fileOperations.onShare?.(file.id, file.name),
      tooltip: t("actions.share", { ns: "common" }),
    });
  }
  if (!readOnly && fileOperations.onStartRename) {
    actions.push({
      icon: <Edit />,
      onClick: () => fileOperations.onStartRename?.(file.id, file.name),
      tooltip: t("actions.rename", { ns: "common" }),
    });
  }
  if (!readOnly && fileOperations.onCut) {
    actions.push({
      icon: <ContentCut />,
      onClick: () => fileOperations.onCut?.(file.id),
      tooltip: t("files:move.cut"),
    });
  }
  if (!readOnly && fileOperations.onRestore) {
    actions.push({
      icon: <Restore />,
      onClick: () => fileOperations.onRestore?.(file.id, file.name),
      tooltip: t("actions.restore", { ns: "common" }),
    });
  }
  if (!readOnly && fileOperations.onDelete) {
    actions.push({
      icon: <Delete />,
      onClick: () => fileOperations.onDelete?.(file.id, file.name),
      tooltip: t("actions.delete", { ns: "common" }),
    });
  }

  return actions;
};

const FileTileItem = ({
  dimmed,
  draggable,
  fileNamePlaceholder,
  fileOperations,
  longPressHandlers,
  onMoveDragStart,
  onToggle,
  readOnly,
  selected,
  selectionMode,
  tile,
  tileSize,
}: {
  tile: FileTile;
  fileOperations: FileOperations;
  fileNamePlaceholder: string;
  tileSize: TilesSize;
  readOnly: boolean;
  selectionMode: boolean;
  selected: boolean;
  onToggle?: (shiftKey: boolean) => void;
  dimmed: boolean;
  draggable: boolean;
  longPressHandlers: TileLongPressHandlers;
  onMoveDragStart?: (event: React.DragEvent<HTMLDivElement>) => void;
}): React.ReactElement => {
  const { t } = useTranslation(["common"]);
  const typeInfo = getFileTypeInfo(tile.file.name, tile.file.contentType, {
    requiresVideoTranscoding: tile.file.requiresVideoTranscoding ?? false,
  });
  const fileEncrypted = isFileEncrypted(
    "metadata" in tile.file ? tile.file.metadata : undefined,
  );
  const isRenamingFile = fileOperations.isRenaming(tile.file.id);
  const iconContainerSx = useFileIconContainerSx(tile.file);

  const fileClick = (event?: React.SyntheticEvent) => {
    const shiftKey = getShiftKey(event);

    if (shiftKey && onToggle) {
      onToggle(true);
      return;
    }
    if (selectionMode) {
      onToggle?.(shiftKey);
      return;
    }
    if (
      (typeInfo.type === "image" || typeInfo.type === "video") &&
      fileOperations.onMediaClick
    ) {
      fileOperations.onMediaClick(tile.file.id);
      return;
    }

    fileOperations.onClick(tile.file.id, tile.file.name, tile.file.sizeBytes);
  };

  return (
    <TileFrame
      dimmed={dimmed}
      draggable={draggable}
      isRenaming={isRenamingFile}
      longPressHandlers={longPressHandlers}
      onMoveDragStart={onMoveDragStart}
      selectionCheckbox={
        <SelectionCheckbox
          onToggle={onToggle}
          selected={selected}
          selectionMode={selectionMode}
        />
      }
    >
      <RenamableItemCard
        variant="squareTile"
        icon={<FilePreviewIcon file={tile.file} tileSize={tileSize} />}
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
        actions={buildFileTileActions({
          file: tile.file,
          fileEncrypted,
          fileOperations,
          readOnly,
          t,
        })}
        isRenaming={isRenamingFile}
        renamingValue={fileOperations.getRenamingName()}
        onRenamingValueChange={fileOperations.onRenamingNameChange}
        onConfirmRename={() => {
          void fileOperations.onConfirmRename?.();
        }}
        onCancelRename={fileOperations.onCancelRename ?? (() => {})}
        placeholder={fileNamePlaceholder}
      />
    </TileFrame>
  );
};

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
    const longPressHandlers = useLongPressSelection({
      onToggle,
      readOnly,
      selectionMode,
    });

    if (tile.kind === "folder") {
      return (
        <FolderTileItem
          tile={tile}
          folderOperations={folderOperations}
          readOnly={readOnly}
          selectionMode={selectionMode}
          selected={selected}
          onToggle={onToggle}
          dimmed={dimmed}
          draggable={draggable}
          dropActive={dropActive}
          longPressHandlers={longPressHandlers}
          onMoveDragStart={onMoveDragStart}
          onMoveDragOver={onMoveDragOver}
          onMoveDragLeave={onMoveDragLeave}
          onMoveDrop={onMoveDrop}
        />
      );
    }

    return (
      <FileTileItem
        tile={tile}
        fileOperations={fileOperations}
        fileNamePlaceholder={fileNamePlaceholder}
        tileSize={tileSize}
        readOnly={readOnly}
        selectionMode={selectionMode}
        selected={selected}
        onToggle={onToggle}
        dimmed={dimmed}
        draggable={draggable}
        longPressHandlers={longPressHandlers}
        onMoveDragStart={onMoveDragStart}
      />
    );
  },
);
