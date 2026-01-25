import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { filesApi } from "../../../shared/api/filesApi";
import { useRenameState } from "../../../shared/hooks/useRenameState";

export const useFileOperations = (onFileDeleted?: () => void) => {
  const { t } = useTranslation(["files", "common"]);
  const confirm = useConfirm();

  const [renameState, renameHandlers] = useRenameState();

  const handleRenameFile = (fileId: string, currentName: string) => {
    renameHandlers.startRename(fileId, currentName);
  };

  const handleConfirmRename = async () => {
    await renameHandlers.confirmRename(async (fileId, newName) => {
      try {
        await filesApi.renameFile(fileId, { name: newName });

        // Trigger parent refresh
        if (onFileDeleted) {
          onFileDeleted();
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
      title: t("deleteFile.confirmTitle", { ns: "files", name: fileName }),
      description: t("deleteFile.confirmDescription", { ns: "files" }),
      confirmationText: t("common:actions.delete"),
      cancellationText: t("common:actions.cancel"),
      confirmationButtonProps: { color: "error" },
    });

    if (!result.confirmed) {
      return;
    }

    await filesApi.deleteFile(fileId);

    // Trigger parent refresh
    if (onFileDeleted) {
      onFileDeleted();
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
