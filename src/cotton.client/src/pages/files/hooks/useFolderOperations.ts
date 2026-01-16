import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { useNodesStore } from "../../../shared/store/nodesStore";

export const useFolderOperations = (currentNodeId: string | null) => {
  const { t } = useTranslation(["files", "common"]);
  const confirm = useConfirm();
  const { createFolder, deleteFolder, renameFolder } = useNodesStore();

  const [isCreatingFolder, setIsCreatingFolder] = useState(false);
  const [newFolderName, setNewFolderName] = useState("");
  const [newFolderParentId, setNewFolderParentId] = useState<string | null>(null);

  const [renamingFolderId, setRenamingFolderId] = useState<string | null>(null);
  const [renamingFolderName, setRenamingFolderName] = useState("");

  const handleNewFolder = () => {
    setNewFolderParentId(currentNodeId);
    setIsCreatingFolder(true);
    setNewFolderName("");
  };

  const handleConfirmNewFolder = async () => {
    const parentId = newFolderParentId;
    if (!parentId || newFolderName.trim().length === 0) {
      setIsCreatingFolder(false);
      setNewFolderName("");
      setNewFolderParentId(null);
      return;
    }
    await createFolder(parentId, newFolderName.trim());
    setIsCreatingFolder(false);
    setNewFolderName("");
    setNewFolderParentId(null);
  };

  const handleCancelNewFolder = () => {
    setIsCreatingFolder(false);
    setNewFolderName("");
    setNewFolderParentId(null);
  };

  const handleRenameFolder = (folderId: string, currentName: string) => {
    setRenamingFolderId(folderId);
    setRenamingFolderName(currentName);
  };

  const handleConfirmRename = async () => {
    if (!renamingFolderId || renamingFolderName.trim().length === 0) {
      setRenamingFolderId(null);
      setRenamingFolderName("");
      return;
    }

    const success = await renameFolder(
      renamingFolderId,
      renamingFolderName.trim(),
      currentNodeId ?? undefined,
    );

    if (success) {
      setRenamingFolderId(null);
      setRenamingFolderName("");
    }
  };

  const handleCancelRename = () => {
    setRenamingFolderId(null);
    setRenamingFolderName("");
  };

  const handleDeleteFolder = async (folderId: string, folderName: string) => {
    try {
      await confirm({
        title: t("delete.confirmTitle", { ns: "files", name: folderName }),
        description: t("delete.confirmDescription", { ns: "files" }),
        confirmationText: t("common:actions.delete"),
        cancellationText: t("common:actions.cancel"),
        confirmationButtonProps: { color: "error" },
      });

      await deleteFolder(folderId, currentNodeId ?? undefined);
    } catch {
      // User cancelled
    }
  };

  return {
    // Create folder state
    isCreatingFolder,
    newFolderName,
    setNewFolderName,
    newFolderParentId,
    handleNewFolder,
    handleConfirmNewFolder,
    handleCancelNewFolder,

    // Rename folder state
    renamingFolderId,
    renamingFolderName,
    setRenamingFolderName,
    handleRenameFolder,
    handleConfirmRename,
    handleCancelRename,

    // Delete folder
    handleDeleteFolder,
  };
};
