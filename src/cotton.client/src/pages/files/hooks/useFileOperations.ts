import { useTranslation } from "react-i18next";
import { filesApi } from "../../../shared/api/filesApi";
import { useFileRenameDeleteOperations } from "../../../shared/hooks/useFileRenameDeleteOperations";
import { useNodesStore } from "../../../shared/store/nodesStore";

export const useFileOperations = (onFilesChanged?: () => void) => {
  const { t } = useTranslation(["files", "common"]);
  const { currentNode, optimisticRenameFile, optimisticDeleteFile, refreshNodeContent } =
    useNodesStore();

  return useFileRenameDeleteOperations({
    getDeleteDialogContent: (fileName) => ({
      title: t("deleteFile.confirmTitle", { ns: "files", name: fileName }),
      description: t("deleteFile.confirmDescription", { ns: "files" }),
      confirmationText: t("common:actions.delete"),
      cancellationText: t("common:actions.cancel"),
    }),
    renameFile: async (fileId, newName) => {
      const parentId = currentNode?.id;

      try {
        if (parentId) {
          optimisticRenameFile(parentId, fileId, newName);
        }

        await filesApi.renameFile(fileId, { name: newName });

        if (parentId) {
          void refreshNodeContent(parentId);
        } else if (onFilesChanged) {
          onFilesChanged();
        }
      } catch (error) {
        // Rollback: reload from server on failure
        if (parentId) {
          void refreshNodeContent(parentId);
        }
        console.error("Failed to rename file:", error);
        return false;
      }
    },
    deleteFile: async (fileId) => {
      const parentId = currentNode?.id;

      if (parentId) {
        optimisticDeleteFile(parentId, fileId);
      }

      try {
        await filesApi.deleteFile(fileId);

        if (parentId) {
          void refreshNodeContent(parentId);
        } else if (onFilesChanged) {
          onFilesChanged();
        }
      } catch (error) {
        // Rollback: reload from server on failure
        if (parentId) {
          void refreshNodeContent(parentId);
        }
        throw error;
      }
    },
    renameErrorMessage: "Failed to rename file:",
    deleteErrorMessage: "Failed to delete file:",
  });
};
