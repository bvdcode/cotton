import React from "react";
import { FolderCard } from "../FolderCard";
import { isFolderEncryptionPolicyEnabled } from "../../../../shared/crypto";
import type {
  FileSystemTile,
  FolderOperations,
} from "@shared/types/FileListViewTypes";
import { TileFrame, SelectionCheckbox } from "./TileSelectionFrame";
import { getShiftKey, type TileLongPressHandlers } from "./tileSelection";

type FolderTile = Extract<FileSystemTile, { kind: "folder" }>;

export const FolderTileItem = ({
  dimmed,
  draggable,
  dropActive,
  folderOperations,
  longPressHandlers,
  onMoveDragLeave,
  onMoveDragOver,
  onMoveDragStart,
  onMoveDrop,
  onToggle,
  readOnly,
  selected,
  selectionMode,
  tile,
}: {
  tile: FolderTile;
  folderOperations: FolderOperations;
  readOnly: boolean;
  selectionMode: boolean;
  selected: boolean;
  onToggle?: (shiftKey: boolean) => void;
  dimmed: boolean;
  draggable: boolean;
  dropActive: boolean;
  longPressHandlers: TileLongPressHandlers;
  onMoveDragStart?: (event: React.DragEvent<HTMLDivElement>) => void;
  onMoveDragOver?: (event: React.DragEvent<HTMLDivElement>) => void;
  onMoveDragLeave?: (event: React.DragEvent<HTMLDivElement>) => void;
  onMoveDrop?: (event: React.DragEvent<HTMLDivElement>) => void;
}): React.ReactElement => {
  const isRenamingFolder = folderOperations.isRenaming(tile.node.id);
  const folderEncryptionPolicy = folderOperations.getEncryptionPolicyState?.(
    tile.node,
  );
  const folderEncrypted =
    folderEncryptionPolicy?.explicitEnabled ??
    isFolderEncryptionPolicyEnabled(tile.node.metadata);

  return (
    <TileFrame
      dimmed={dimmed}
      draggable={draggable}
      dropActive={dropActive}
      isRenaming={isRenamingFolder}
      longPressHandlers={longPressHandlers}
      onMoveDragStart={onMoveDragStart}
      onMoveDragOver={onMoveDragOver}
      onMoveDragLeave={onMoveDragLeave}
      onMoveDrop={onMoveDrop}
      selectionCheckbox={
        <SelectionCheckbox
          onToggle={onToggle}
          selected={selected}
          selectionMode={selectionMode}
        />
      }
    >
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
            ? () =>
                folderOperations.onStartRename?.(tile.node.id, tile.node.name)
            : undefined
        }
        onDelete={
          folderOperations.onDelete
            ? () => folderOperations.onDelete?.(tile.node.id, tile.node.name)
            : undefined
        }
        onDownload={
          folderOperations.onDownload
            ? () => folderOperations.onDownload?.(tile.node.id, tile.node.name)
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
        onTogglePin={
          folderOperations.onTogglePin
            ? () => folderOperations.onTogglePin?.(tile.node.id)
            : undefined
        }
        onShowInfo={
          folderOperations.onShowInfo
            ? () => folderOperations.onShowInfo?.(tile.node)
            : undefined
        }
        isPinned={folderOperations.isPinned?.(tile.node.id) ?? false}
        onToggleEncryptionPolicy={
          folderOperations.onToggleEncryptionPolicy
            ? () =>
                folderOperations.onToggleEncryptionPolicy?.(
                  tile.node.id,
                  folderEncrypted,
                )
            : undefined
        }
        onRestore={
          folderOperations.onRestore
            ? () => folderOperations.onRestore?.(tile.node.id, tile.node.name)
            : undefined
        }
        onClick={(event) => {
          const shiftKey = getShiftKey(event);

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
    </TileFrame>
  );
};
