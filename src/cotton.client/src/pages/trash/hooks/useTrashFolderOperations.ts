import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { useRenameState } from "../../../shared/hooks/useRenameState";

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

  const [renameState, renameHandlers] = useRenameState();

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
