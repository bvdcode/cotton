import { useTranslation } from "react-i18next";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { nodesApi } from "../../../shared/api/nodesApi";
import { useFolderRenameDeleteOperations } from "../../../shared/hooks/useFolderRenameDeleteOperations";

/**
 * Hook for trash folder operations - similar to useFolderOperations
 * but uses skipTrash=true when deleting.
 *
 * @param resolveWrapperNodeId - When at the trash root, maps a displayed
 *   folder ID to the wrapper node ID that should actually be deleted.
 */
export const useTrashFolderOperations = (
  currentNodeId: string | null,
  onDeleted?: () => void,
  resolveWrapperNodeId?: (itemId: string) => string | null,
) => {
  const { t } = useTranslation(["trash", "common"]);
  const { deleteFolder, renameFolder } = useNodesStore();

  return useFolderRenameDeleteOperations({
    getDeleteDialogContent: (folderName) => ({
      title: t("deleteFolderForever.confirmTitle", {
        ns: "trash",
        name: folderName,
      }),
      description: t("deleteFolderForever.confirmDescription", {
        ns: "trash",
      }),
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
      const wrapperId = resolveWrapperNodeId?.(folderId);
      if (wrapperId) {
        // Delete the wrapper node (permanent), which cascades to its contents
        await nodesApi.deleteNode(wrapperId, true);
      } else {
        // Pass skipTrash=true for permanent deletion
        await deleteFolder(folderId, currentNodeId ?? undefined, true);
      }

      if (onDeleted) {
        onDeleted();
      }
    },
    renameErrorMessage: "Failed to rename folder:",
    deleteErrorMessage: "Failed to delete folder permanently:",
  });
};
