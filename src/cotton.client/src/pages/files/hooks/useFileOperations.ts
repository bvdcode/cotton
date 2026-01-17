import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { filesApi } from "../../../shared/api/filesApi";

export const useFileOperations = (onFileDeleted?: () => void) => {
  const { t } = useTranslation(["files", "common"]);
  const confirm = useConfirm();

  const [renamingFileId, setRenamingFileId] = useState<string | null>(null);
  const [renamingFileName, setRenamingFileName] = useState("");

  const handleRenameFile = (fileId: string, currentName: string) => {
    setRenamingFileId(fileId);
    setRenamingFileName(currentName);
  };

  const handleConfirmRename = async () => {
    const fileId = renamingFileId;
    if (!fileId || renamingFileName.trim().length === 0) {
      setRenamingFileId(null);
      setRenamingFileName("");
      return;
    }

    try {
      await filesApi.renameFile(fileId, { name: renamingFileName.trim() });
      setRenamingFileId(null);
      setRenamingFileName("");

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
