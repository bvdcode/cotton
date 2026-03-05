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
  onFolderShare?: (folderId: string, folderName: string) => Promise<void>,
): FolderOperations => {
  return {
    isRenaming: (folderId: string) => folderOps.renamingFolderId === folderId,
    getRenamingName: () => folderOps.renamingFolderName,
    onRenamingNameChange: folderOps.setRenamingFolderName,
    onConfirmRename: () => {
      void folderOps.handleConfirmRename();
    },
    onCancelRename: folderOps.handleCancelRename,
    onStartRename: folderOps.handleRenameFolder,
    onDelete: (folderId: string, folderName: string) => {
      void folderOps.handleDeleteFolder(folderId, folderName);
    },
    onShare: onFolderShare
      ? (folderId: string, folderName: string) => {
          void onFolderShare(folderId, folderName);
        }
      : undefined,
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
    onDownload?: (fileId: string, fileName: string) => Promise<void>;
    onShare?: (fileId: string, fileName: string) => Promise<void>;
    onClick: (fileId: string, fileName: string, fileSizeBytes?: number) => void;
    onMediaClick?: (fileId: string) => void;
  },
): FileOperations => {
  return {
    isRenaming: (fileId: string) => fileOps.renamingFileId === fileId,
    getRenamingName: () => fileOps.renamingFileName,
    onRenamingNameChange: fileOps.setRenamingFileName,
    onConfirmRename: fileOps.handleConfirmRename,
    onCancelRename: fileOps.handleCancelRename,
    onStartRename: fileOps.handleRenameFile,
    onDelete: (fileId: string, fileName: string) => {
      void fileOps.handleDeleteFile(fileId, fileName);
    },
    onDownload: handlers.onDownload
      ? (fileId: string, fileName: string) => {
          void handlers.onDownload?.(fileId, fileName);
        }
      : undefined,
    onShare: handlers.onShare
      ? (fileId: string, fileName: string) => {
          void handlers.onShare?.(fileId, fileName);
        }
      : undefined,
    onClick: handlers.onClick,
    onMediaClick: handlers.onMediaClick,
  };
};
