import React from "react";
import { getFolderIcon } from "@shared/utils/icons";
import { RenamableItemCard } from "../RenamableItemCard";

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
  <RenamableItemCard
    icon={getFolderIcon()}
    title={newFolderName}
    variant="squareTile"
    isRenaming
    renamingValue={newFolderName}
    onRenamingValueChange={onNewFolderNameChange}
    onConfirmRename={onConfirmNewFolder}
    onCancelRename={onCancelNewFolder}
    placeholder={folderNamePlaceholder}
  />
);
