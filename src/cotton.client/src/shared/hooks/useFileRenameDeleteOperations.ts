import { useConfirm } from "material-ui-confirm";
import { useRenameState } from "./useRenameState";

interface ConfirmDialogContent {
  title: string;
  description: string;
  confirmationText: string;
  cancellationText: string;
}

interface UseFileRenameDeleteOperationsOptions {
  getDeleteDialogContent: (fileName: string) => ConfirmDialogContent;
  renameFile: (fileId: string, newName: string) => Promise<boolean | void>;
  deleteFile: (fileId: string) => Promise<void>;
  renameErrorMessage: string;
  deleteErrorMessage: string;
}

export const useFileRenameDeleteOperations = (
  options: UseFileRenameDeleteOperationsOptions,
) => {
  const confirm = useConfirm();
  const [renameState, renameHandlers] = useRenameState();

  const handleRenameFile = (fileId: string, currentName: string) => {
    renameHandlers.startRename(fileId, currentName);
  };

  const handleConfirmRename = async () => {
    await renameHandlers.confirmRename(async (fileId, newName) => {
      try {
        return await options.renameFile(fileId, newName);
      } catch (error) {
        console.error(options.renameErrorMessage, error);
        return false;
      }
    });
  };

  const handleCancelRename = () => {
    renameHandlers.cancelRename();
  };

  const handleDeleteFile = async (fileId: string, fileName: string) => {
    const dialogContent = options.getDeleteDialogContent(fileName);

    try {
      const result = await confirm({
        ...dialogContent,
        confirmationButtonProps: { color: "error" },
      });

      if (!result.confirmed) {
        return;
      }

      await options.deleteFile(fileId);
    } catch (error) {
      console.error(options.deleteErrorMessage, error);
    }
  };

  return {
    renamingFileId: renameState.renamingId,
    renamingFileName: renameState.renamingName,
    setRenamingFileName: renameHandlers.setRenamingName,
    handleRenameFile,
    handleConfirmRename,
    handleCancelRename,
    handleDeleteFile,
  };
};
