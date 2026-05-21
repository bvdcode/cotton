import type {
  FolderOperations,
  FileOperations,
} from "@shared/types/FileListViewTypes";
import type { FolderEncryptionPolicyState } from "@shared/crypto";
import type { NodeDto } from "@shared/api/layoutsApi";

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
  onFolderCut?: (folderId: string) => void,
  onToggleEncryptionPolicy?: (
    folderId: string,
    currentlyEnabled: boolean,
  ) => Promise<void> | void,
  getEncryptionPolicyState?: (
    folder: NodeDto,
  ) => FolderEncryptionPolicyState,
  onFolderDownload?: (folderId: string, folderName: string) => Promise<void>,
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
    onDownload: onFolderDownload
      ? (folderId: string, folderName: string) => {
          void onFolderDownload(folderId, folderName);
        }
      : undefined,
    onShare: onFolderShare
      ? (folderId: string, folderName: string) => {
          void onFolderShare(folderId, folderName);
        }
      : undefined,
    onCut: onFolderCut,
    onToggleEncryptionPolicy: onToggleEncryptionPolicy
      ? (folderId, currentlyEnabled) => {
          void onToggleEncryptionPolicy(folderId, currentlyEnabled);
        }
      : undefined,
    getEncryptionPolicyState,
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
    onVersions?: (fileId: string, fileName: string) => void;
    onShare?: (fileId: string, fileName: string) => Promise<void>;
    onCut?: (fileId: string) => void;
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
    onVersions: handlers.onVersions,
    onShare: handlers.onShare
      ? (fileId: string, fileName: string) => {
          void handlers.onShare?.(fileId, fileName);
        }
      : undefined,
    onCut: handlers.onCut,
    onClick: handlers.onClick,
    onMediaClick: handlers.onMediaClick,
  };
};
