import type {
  FolderOperations,
  FileOperations,
} from "../../pages/files/types/FileListViewTypes";

/**
 * Build folder operations adapter from hook
 */
export const buildFolderOperations = (
  folderOps: {
    renamingFolderId: string | null;
    renamingFolderName: string;
    setRenamingFolderName: (name: string) => void;
    handleRenameFolder: (folderId: string, currentName: string) => void;
    handleConfirmRename: () => Promise<void>;
    handleCancelRename: () => void;
    handleDeleteFolder: (folderId: string, folderName: string) => Promise<void>;
  },
  onFolderClick: (folderId: string) => void,
): FolderOperations => {
  return {
    isRenaming: (folderId: string) => folderOps.renamingFolderId === folderId,
    getRenamingName: () => folderOps.renamingFolderName,
    onRenamingNameChange: folderOps.setRenamingFolderName,
    onConfirmRename: folderOps.handleConfirmRename,
    onCancelRename: folderOps.handleCancelRename,
    onStartRename: folderOps.handleRenameFolder,
    onDelete: folderOps.handleDeleteFolder,
    onClick: onFolderClick,
  };
};

/**
 * Build file operations adapter from hook
 */
export const buildFileOperations = (
  fileOps: {
    renamingFileId: string | null;
    renamingFileName: string;
    setRenamingFileName: (name: string) => void;
    handleRenameFile: (fileId: string, currentName: string) => void;
    handleConfirmRename: () => Promise<void>;
    handleCancelRename: () => void;
    handleDeleteFile: (fileId: string, fileName: string) => Promise<void>;
  },
  handlers: {
    onDownload: (fileId: string, fileName: string) => Promise<void>;
    onClick: (fileId: string, fileName: string, fileSizeBytes?: number) => void;
    onMediaClick: (fileId: string) => void;
  },
): FileOperations => {
  return {
    isRenaming: (fileId: string) => fileOps.renamingFileId === fileId,
    getRenamingName: () => fileOps.renamingFileName,
    onRenamingNameChange: fileOps.setRenamingFileName,
    onConfirmRename: fileOps.handleConfirmRename,
    onCancelRename: fileOps.handleCancelRename,
    onStartRename: fileOps.handleRenameFile,
    onDelete: fileOps.handleDeleteFile,
    onDownload: handlers.onDownload,
    onClick: handlers.onClick,
    onMediaClick: handlers.onMediaClick,
  };
};
