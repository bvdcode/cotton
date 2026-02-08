import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { filesApi } from "../../../shared/api/filesApi";
import { useRenameState } from "../../../shared/hooks/useRenameState";
import { useNodesStore } from "../../../shared/store/nodesStore";

export const useFileOperations = (onFilesChanged?: () => void) => {
  const { t } = useTranslation(["files", "common"]);
  const confirm = useConfirm();
  const { currentNode, optimisticRenameFile, optimisticDeleteFile, refreshNodeContent } =
    useNodesStore();

  const [renameState, renameHandlers] = useRenameState();

  const handleRenameFile = (fileId: string, currentName: string) => {
    renameHandlers.startRename(fileId, currentName);
  };

  const handleConfirmRename = async () => {
    await renameHandlers.confirmRename(async (fileId, newName) => {
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
    });
  };

  const handleCancelRename = () => {
    renameHandlers.cancelRename();
  };

  const handleDeleteFile = async (fileId: string, fileName: string) => {
    const result = await confirm({
      title: t("deleteFile.confirmTitle", { ns: "files", name: fileName }),
      description: t("deleteFile.confirmDescription", { ns: "files" }),
      confirmationText: t("common:actions.delete"),
      cancellationText: t("common:actions.cancel"),
      confirmationButtonProps: { color: "error" },
    });

    if (!result.confirmed) {
      return;
    }

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
      console.error("Failed to delete file:", error);
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
