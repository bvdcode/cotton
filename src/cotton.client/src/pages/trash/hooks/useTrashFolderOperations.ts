import { useTranslation } from "react-i18next";
import { nodesApi } from "../../../shared/api/nodesApi";
import { deleteFolder, renameFolder } from "../../../shared/store/nodesActions";
import { useFolderRenameDeleteOperations } from "../../../shared/hooks/useFolderRenameDeleteOperations";

export const useTrashFolderOperations = (
  currentNodeId: string | null,
  onDeleted?: () => void,
  resolveWrapperNodeId?: (itemId: string) => string | null,
) => {
  const { t } = useTranslation(["trash", "common"]);

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
      return await renameFolder(folderId, newName, currentNodeId ?? undefined);
    },
    deleteFolder: async (folderId) => {
      const wrapperId = resolveWrapperNodeId?.(folderId);
      if (wrapperId) {
        await nodesApi.deleteNode(wrapperId, true);
      } else {
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
