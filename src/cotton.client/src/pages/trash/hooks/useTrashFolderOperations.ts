import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { useNodesStore } from "../../../shared/store/nodesStore";

/**
 * Hook for trash folder operations - similar to useFolderOperations
 * but uses skipTrash=true when deleting
 */
export const useTrashFolderOperations = (
  currentNodeId: string | null,
  onDeleted?: () => void,
) => {
  const { t } = useTranslation(["trash", "common"]);
  const confirm = useConfirm();
  const { deleteFolder, renameFolder } = useNodesStore();

  const [renamingFolderId, setRenamingFolderId] = useState<string | null>(null);
  const [renamingFolderName, setRenamingFolderName] = useState("");
  const [originalFolderName, setOriginalFolderName] = useState("");

  const handleRenameFolder = (folderId: string, currentName: string) => {
    setRenamingFolderId(folderId);
    setRenamingFolderName(currentName);
    setOriginalFolderName(currentName);
  };

  const handleConfirmRename = async () => {
    if (!renamingFolderId || renamingFolderName.trim().length === 0) {
      setRenamingFolderId(null);
      setRenamingFolderName("");
      setOriginalFolderName("");
      return;
    }

    const newName = renamingFolderName.trim();

    // No changes - just close rename mode
    if (newName === originalFolderName) {
      setRenamingFolderId(null);
      setRenamingFolderName("");
      setOriginalFolderName("");
      return;
    }

    const success = await renameFolder(
      renamingFolderId,
      newName,
      currentNodeId ?? undefined,
    );

    if (success) {
      setRenamingFolderId(null);
      setRenamingFolderName("");
      setOriginalFolderName("");
    }
  };

  const handleCancelRename = () => {
    setRenamingFolderId(null);
    setRenamingFolderName("");
    setOriginalFolderName("");
  };

  const handleDeleteFolder = async (folderId: string, folderName: string) => {
    try {
      const result = await confirm({
        title: t("deleteFolderForever.confirmTitle", {
          ns: "trash",
          name: folderName,
        }),
        description: t("deleteFolderForever.confirmDescription", {
          ns: "trash",
        }),
        confirmationText: t("common:actions.delete"),
        cancellationText: t("common:actions.cancel"),
        confirmationButtonProps: { color: "error" },
      });

      if (result.confirmed) {
        // Pass skipTrash=true for permanent deletion
        await deleteFolder(folderId, currentNodeId ?? undefined, true);

        // Trigger parent refresh
        if (onDeleted) {
          onDeleted();
        }
      }
    } catch {
      // User cancelled
    }
  };

  return {
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
