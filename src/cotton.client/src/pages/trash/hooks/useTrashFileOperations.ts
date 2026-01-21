import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { filesApi } from "../../../shared/api/filesApi";

/**
 * Hook for trash file operations - similar to useFileOperations
 * but uses skipTrash=true when deleting
 */
export const useTrashFileOperations = (onFileDeleted?: () => void) => {
  const { t } = useTranslation(["trash", "common"]);
  const confirm = useConfirm();

  const [renamingFileId, setRenamingFileId] = useState<string | null>(null);
  const [renamingFileName, setRenamingFileName] = useState("");
  const [originalFileName, setOriginalFileName] = useState("");

  const handleRenameFile = (fileId: string, currentName: string) => {
    setRenamingFileId(fileId);
    setRenamingFileName(currentName);
    setOriginalFileName(currentName);
  };

  const handleConfirmRename = async () => {
    const fileId = renamingFileId;
    if (!fileId || renamingFileName.trim().length === 0) {
      setRenamingFileId(null);
      setRenamingFileName("");
      setOriginalFileName("");
      return;
    }

    const newName = renamingFileName.trim();

    // No changes - just close rename mode
    if (newName === originalFileName) {
      setRenamingFileId(null);
      setRenamingFileName("");
      setOriginalFileName("");
      return;
    }

    try {
      await filesApi.renameFile(fileId, { name: newName });
      setRenamingFileId(null);
      setRenamingFileName("");
      setOriginalFileName("");

      // Trigger parent refresh
      if (onFileDeleted) {
        onFileDeleted();
      }
    } catch (error) {
      console.error("Failed to rename file:", error);
    }
  };

  const handleCancelRename = () => {
    setRenamingFileId(null);
    setRenamingFileName("");
    setOriginalFileName("");
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

    // Pass skipTrash=true for permanent deletion
    await filesApi.deleteFile(fileId, true);

    // Trigger parent refresh
    if (onFileDeleted) {
      onFileDeleted();
    }
  };

  return {
    // Rename file state
    renamingFileId,
    renamingFileName,
    setRenamingFileName,
    handleRenameFile,
    handleConfirmRename,
    handleCancelRename,

    // Delete file
    handleDeleteFile,
  };
};
