import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { useFolderRenameDeleteOperations } from "../../../shared/hooks/useFolderRenameDeleteOperations";

export const useFolderOperations = (
  currentNodeId: string | null,
  onFolderChanged?: () => void,
) => {
  const { t } = useTranslation(["files", "common"]);
  const { createFolder, deleteFolder, renameFolder } = useNodesStore();

  const [isCreatingFolder, setIsCreatingFolder] = useState(false);
  const [newFolderName, setNewFolderName] = useState("");
  const [newFolderParentId, setNewFolderParentId] = useState<string | null>(
    null,
  );

  const renameDelete = useFolderRenameDeleteOperations({
    getDeleteDialogContent: (folderName) => ({
      title: t("deleteFolder.confirmTitle", {
        ns: "files",
        name: folderName,
      }),
      description: t("deleteFolder.confirmDescription", { ns: "files" }),
      confirmationText: t("common:actions.delete"),
      cancellationText: t("common:actions.cancel"),
    }),
    renameFolder: async (folderId, newName) => {
      return await renameFolder(
        folderId,
        newName,
        currentNodeId ?? undefined,
      );
    },
    deleteFolder: async (folderId) => {
      await deleteFolder(folderId, currentNodeId ?? undefined);
      onFolderChanged?.();
    },
    renameErrorMessage: "Failed to rename folder:",
    deleteErrorMessage: "Failed to delete folder:",
  });

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
    onFolderChanged?.();
    setIsCreatingFolder(false);
    setNewFolderName("");
    setNewFolderParentId(null);
  };

  const handleCancelNewFolder = () => {
    setIsCreatingFolder(false);
    setNewFolderName("");
    setNewFolderParentId(null);
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
    renamingFolderId: renameDelete.renamingFolderId,
    renamingFolderName: renameDelete.renamingFolderName,
    setRenamingFolderName: renameDelete.setRenamingFolderName,
    handleRenameFolder: renameDelete.handleRenameFolder,
    handleConfirmRename: renameDelete.handleConfirmRename,
    handleCancelRename: renameDelete.handleCancelRename,

    // Delete folder
    handleDeleteFolder: renameDelete.handleDeleteFolder,
  };
};
