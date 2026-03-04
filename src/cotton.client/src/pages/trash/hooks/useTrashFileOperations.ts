import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { filesApi } from "../../../shared/api/filesApi";
import { nodesApi } from "../../../shared/api/nodesApi";
import { useRenameState } from "../../../shared/hooks/useRenameState";

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
  const confirm = useConfirm();

  const [renameState, renameHandlers] = useRenameState();

  const handleRenameFile = (fileId: string, currentName: string) => {
    renameHandlers.startRename(fileId, currentName);
  };

  const handleConfirmRename = async () => {
    await renameHandlers.confirmRename(async (fileId, newName) => {
      try {
        await filesApi.renameFile(fileId, { name: newName });

        if (onFilesChanged) {
          onFilesChanged();
        }
      } catch (error) {
        console.error("Failed to rename file:", error);
        return false;
      }
    });
  };

  const handleCancelRename = () => {
    renameHandlers.cancelRename();
  };

  const handleDeleteFile = async (fileId: string, fileName: string) => {
    const result = await confirm({
      title: t("deleteFile.confirmTitle", { ns: "trash", name: fileName }),
      description: t("deleteFile.confirmDescription", { ns: "trash" }),
      confirmationText: t("common:actions.delete"),
      cancellationText: t("common:actions.cancel"),
      confirmationButtonProps: { color: "error" },
    });

    if (!result.confirmed) {
      return;
    }

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
  };

  return {
    // Rename file state
    renamingFileId: renameState.renamingId,
    renamingFileName: renameState.renamingName,
    setRenamingFileName: renameHandlers.setRenamingName,
    handleRenameFile,
    handleConfirmRename,
    handleCancelRename,

    // Delete file
    handleDeleteFile,
  };
};
