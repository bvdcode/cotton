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
    const confirmed = await Promise.resolve(
      confirm({
        title: t("deleteFile.confirmTitle", { ns: "files", name: fileName }),
        description: t("deleteFile.confirmDescription", { ns: "files" }),
        confirmationText: t("common:actions.delete"),
        cancellationText: t("common:actions.cancel"),
        confirmationButtonProps: { color: "error" },
      }),
    )
      .then((result) => {
        if (typeof result === "boolean") return result;
        if (result && typeof result === "object" && "confirmed" in result) {
          return Boolean((result as { confirmed?: unknown }).confirmed);
        }
        return true;
      })
      .catch(() => false);

    if (!confirmed) return;

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
