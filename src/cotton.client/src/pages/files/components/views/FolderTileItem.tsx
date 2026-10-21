import React from "react";
import { LockOutlined, Star } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { FolderCard } from "../FolderCard";
import { isFolderEncryptionPolicyEnabled } from "../../../../shared/crypto";
import { hasEncryptedFolderName } from "@shared/crypto/folderDisplayMeta";
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
  const { t } = useTranslation(["common", "home"]);
  const isRenamingFolder = folderOperations.isRenaming(tile.node.id);
  const folderEncryptionPolicy = folderOperations.getEncryptionPolicyState?.(
    tile.node,
  );
  const folderEncrypted =
    folderEncryptionPolicy?.explicitEnabled ??
    isFolderEncryptionPolicyEnabled(tile.node.metadata);
  const effectiveEncryption =
    (folderEncryptionPolicy?.effectiveEnabled ?? folderEncrypted) ||
    hasEncryptedFolderName(tile.node.metadata);
  const isPinned = folderOperations.isPinned?.(tile.node.id) ?? false;

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
        cornerAdornment={
          (effectiveEncryption || isPinned) && (
            <>
              {effectiveEncryption && (
                <LockOutlined
                  titleAccess={t(
                    "common:clientEncryption.folderPolicyEnabledHint",
                  )}
                />
              )}
              {isPinned && (
                <Star titleAccess={t("home:dashboard.widgets.pinnedFolders")} />
              )}
            </>
          )
        }
        encryptionPolicy={folderEncryptionPolicy}
        isRenaming={isRenamingFolder}
        renamingName={folderOperations.getRenamingName()}
        onRenamingNameChange={folderOperations.onRenamingNameChange}
        onConfirmRename={folderOperations.onConfirmRename}
        onCancelRename={folderOperations.onCancelRename}
        onStartRename={folderOperations.onStartRename?.bind(
          folderOperations,
          tile.node.id,
          tile.node.name,
        )}
        onDelete={folderOperations.onDelete?.bind(
          folderOperations,
          tile.node.id,
          tile.node.name,
        )}
        onDownload={folderOperations.onDownload?.bind(
          folderOperations,
          tile.node.id,
          tile.node.name,
        )}
        onShare={folderOperations.onShare?.bind(
          folderOperations,
          tile.node.id,
          tile.node.name,
        )}
        onCopy={folderOperations.onCopy?.bind(folderOperations, tile.node.id)}
        onCut={folderOperations.onCut?.bind(folderOperations, tile.node.id)}
        onTogglePin={folderOperations.onTogglePin?.bind(
          folderOperations,
          tile.node.id,
        )}
        onShowInfo={folderOperations.onShowInfo?.bind(
          folderOperations,
          tile.node,
        )}
        isPinned={isPinned}
        onToggleEncryptionPolicy={folderOperations.onToggleEncryptionPolicy?.bind(
          folderOperations,
          tile.node.id,
          folderEncrypted,
        )}
        onRestore={folderOperations.onRestore?.bind(
          folderOperations,
          tile.node.id,
          tile.node.name,
        )}
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
