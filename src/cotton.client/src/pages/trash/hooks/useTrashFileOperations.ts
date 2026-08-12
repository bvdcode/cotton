import { useTranslation } from "react-i18next";
import { filesApi } from "../../../shared/api/filesApi";
import { nodesApi } from "../../../shared/api/nodesApi";
import { useFileRenameDeleteOperations } from "../../../shared/hooks/useFileRenameDeleteOperations";

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
        await nodesApi.deleteNode(wrapperId, true);
      } else {
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
