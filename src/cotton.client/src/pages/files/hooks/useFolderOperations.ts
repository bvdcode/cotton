import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { useRenameState } from "../../../shared/hooks/useRenameState";

export const useFolderOperations = (currentNodeId: string | null) => {
  const { t } = useTranslation(["files", "common"]);
  const confirm = useConfirm();
  const { createFolder, deleteFolder, renameFolder } = useNodesStore();

  const [isCreatingFolder, setIsCreatingFolder] = useState(false);
  const [newFolderName, setNewFolderName] = useState("");
  const [newFolderParentId, setNewFolderParentId] = useState<string | null>(
    null,
  );

  const [renameState, renameHandlers] = useRenameState();

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
    renameHandlers.startRename(folderId, currentName);
  };

  const handleConfirmRename = async () => {
    await renameHandlers.confirmRename(async (folderId, newName) => {
      return await renameFolder(
        folderId,
        newName,
        currentNodeId ?? undefined,
      );
    });
  };

  const handleCancelRename = () => {
    renameHandlers.cancelRename();
  };

  const handleDeleteFolder = async (folderId: string, folderName: string) => {
    try {
      const result = await confirm({
        title: t("deleteFolder.confirmTitle", {
          ns: "files",
          name: folderName,
        }),
        description: t("deleteFolder.confirmDescription", { ns: "files" }),
        confirmationText: t("common:actions.delete"),
        cancellationText: t("common:actions.cancel"),
        confirmationButtonProps: { color: "error" },
      });

      if (result.confirmed) {
        await deleteFolder(folderId, currentNodeId ?? undefined);
      }
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
    renamingFolderId: renameState.renamingId,
    renamingFolderName: renameState.renamingName,
    setRenamingFolderName: renameHandlers.setRenamingName,
    handleRenameFolder,
    handleConfirmRename,
    handleCancelRename,

    // Delete folder
    handleDeleteFolder,
  };
};
