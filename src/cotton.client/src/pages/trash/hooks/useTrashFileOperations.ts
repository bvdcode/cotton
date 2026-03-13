import { useTranslation } from "react-i18next";
import { filesApi } from "../../../shared/api/filesApi";
import { nodesApi } from "../../../shared/api/nodesApi";
import { useFileRenameDeleteOperations } from "../../../shared/hooks/useFileRenameDeleteOperations";

/**
 * Hook for trash file operations - similar to useFileOperations
 * but uses skipTrash=true when deleting.
 *
 * @param resolveWrapperNodeId - When at the trash root, maps a displayed
 *   file ID to the wrapper node ID that should actually be deleted.
 */
export const useTrashFileOperations = (
  onFilesChanged?: () => void,
  resolveWrapperNodeId?: (itemId: string) => string | null,
) => {
  const { t } = useTranslation(["trash", "common"]);

  return useFileRenameDeleteOperations({
    getDeleteDialogContent: (fileName) => ({
      title: t("deleteFile.confirmTitle", { ns: "trash", name: fileName }),
      description: t("deleteFile.confirmDescription", { ns: "trash" }),
      confirmationText: t("common:actions.delete"),
      cancellationText: t("common:actions.cancel"),
    }),
    renameFile: async (fileId, newName) => {
      try {
        await filesApi.renameFile(fileId, { name: newName });

        if (onFilesChanged) {
          onFilesChanged();
        }
      } catch (error) {
        console.error("Failed to rename file:", error);
        return false;
      }
    },
    deleteFile: async (fileId) => {
      const wrapperId = resolveWrapperNodeId?.(fileId);
      if (wrapperId) {
        // Delete the wrapper node (permanent), which cascades to its contents
        await nodesApi.deleteNode(wrapperId, true);
      } else {
        // Pass skipTrash=true for permanent deletion
        await filesApi.deleteFile(fileId, true);
      }

      if (onFilesChanged) {
        onFilesChanged();
      }
    },
    renameErrorMessage: "Failed to rename file:",
    deleteErrorMessage: "Failed to delete file permanently:",
  });
};
