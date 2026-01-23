import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { filesApi } from "../../../shared/api/filesApi";
import { nodesApi } from "../../../shared/api/nodesApi";

/**
 * Hook for trash file operations - similar to useFileOperations
 * but uses skipTrash=true when deleting and handles wrapper nodes
 * 
 * Following Single Responsibility Principle:
 * - Manages file operations UI state
 * - Delegates deletion logic and wrapper resolution to services
 */
export const useTrashFileOperations = (
  wrapperMap: Map<string, string>,
  onFileDeleted?: () => void,
) => {
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

    try {
      // Check if file is inside a wrapper node
      if (wrapperMap.has(fileId)) {
        // File is inside a wrapper - delete the wrapper node instead of the file
        const wrapperNodeId = wrapperMap.get(fileId)!;
        console.log(
          `Deleting wrapper node ${wrapperNodeId} for file ${fileId}`,
        );
        await nodesApi.deleteNode(wrapperNodeId, true);
      } else {
        // Regular file deletion (not wrapped)
        console.log(`Deleting file ${fileId} directly`);
        await filesApi.deleteFile(fileId, true);
      }

      // Trigger parent refresh
      if (onFileDeleted) {
        onFileDeleted();
      }
    } catch (error) {
      console.error("Failed to delete file:", error);
      throw error;
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
