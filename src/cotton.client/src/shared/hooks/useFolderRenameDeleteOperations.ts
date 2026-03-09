import { useConfirm } from "material-ui-confirm";
import { useRenameState } from "./useRenameState";

interface ConfirmDialogContent {
  title: string;
  description: string;
  confirmationText: string;
  cancellationText: string;
}

interface UseFolderRenameDeleteOperationsOptions {
  getDeleteDialogContent: (folderName: string) => ConfirmDialogContent;
  renameFolder: (folderId: string, newName: string) => Promise<boolean | void>;
  deleteFolder: (folderId: string) => Promise<void>;
  renameErrorMessage: string;
  deleteErrorMessage: string;
}

export const useFolderRenameDeleteOperations = (
  options: UseFolderRenameDeleteOperationsOptions,
) => {
  const confirm = useConfirm();
  const [renameState, renameHandlers] = useRenameState();

  const handleRenameFolder = (folderId: string, currentName: string) => {
    renameHandlers.startRename(folderId, currentName);
  };

  const handleConfirmRename = async () => {
    await renameHandlers.confirmRename(async (folderId, newName) => {
      try {
        return await options.renameFolder(folderId, newName);
      } catch (error) {
        console.error(options.renameErrorMessage, error);
        return false;
      }
    });
  };

  const handleCancelRename = () => {
    renameHandlers.cancelRename();
  };

  const handleDeleteFolder = async (folderId: string, folderName: string) => {
    const dialogContent = options.getDeleteDialogContent(folderName);

    try {
      const result = await confirm({
        ...dialogContent,
        confirmationButtonProps: { color: "error" },
      });

      if (!result.confirmed) {
        return;
      }

      await options.deleteFolder(folderId);
    } catch (error) {
      console.error(options.deleteErrorMessage, error);
    }
  };

  return {
    renamingFolderId: renameState.renamingId,
    renamingFolderName: renameState.renamingName,
    setRenamingFolderName: renameHandlers.setRenamingName,
    handleRenameFolder,
    handleConfirmRename,
    handleCancelRename,
    handleDeleteFolder,
  };
};
