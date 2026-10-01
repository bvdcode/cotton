import type React from "react";
import type { FolderEncryptionPolicyState } from "@shared/crypto";
import type { NodeDto } from "@shared/api/layoutsApi";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";

export interface FileListRow {
  id: string;
  type: "folder" | "file" | "new-folder";
  name: string;
  location?: string | null;
  containerPath?: string | null;
  containerNodeId?: string | null;
  sizeBytes: number | null;
  contentType?: string | null;
  metadata?: Record<string, string>;
  encryptionPolicy?: FolderEncryptionPolicyState;
  requiresVideoTranscoding?: boolean;
  tile?: FileSystemTile;
}

export interface ColumnOptions {
  readOnly?: boolean;
  labels: {
    name: string;
    size: string;
    location: string;
    actionsTitle: string;
    placeholder: string;
    goToFolder: string;
    rename: string;
    delete: string;
    restore: string;
    download: string;
    versions: string;
    share: string;
    cut: string;
    encryptedFile: string;
    encryptedFolder: string;
    enableEncryptionPolicy: string;
    disableEncryptionPolicy: string;
    pin: string;
    unpin: string;
    info: string;
  };
  newFolderName: string;
  onNewFolderNameChange: (value: string) => void;
  onConfirmNewFolder: () => void;
  onCancelNewFolder: () => void;
  folderNamePlaceholder: string;
  fileNamePlaceholder: string;
  onGoToFileLocation?: (target: {
    nodeId?: string;
    containerPath?: string;
  }) => void;
  columnFlex?: {
    name: number;
    location: number;
  };
  folderOperations: {
    isRenaming: (id: string) => boolean;
    getRenamingName: () => string;
    onRenamingNameChange: (value: string) => void;
    onConfirmRename?: () => void;
    onCancelRename?: () => void;
    onStartRename?: (id: string, name: string) => void;
    onRestore?: (id: string, name: string) => void;
    onDelete?: (id: string, name: string) => void;
    onDownload?: (id: string, name: string) => void;
    onShare?: (id: string, name: string) => void;
    onCut?: (id: string) => void;
    onToggleEncryptionPolicy?: (id: string, currentlyEnabled: boolean) => void;
    onTogglePin?: (id: string) => void;
    isPinned?: (id: string) => boolean;
    onShowInfo?: (folder: NodeDto) => void;
  };
  fileOperations: {
    isRenaming: (id: string) => boolean;
    getRenamingName: () => string;
    onRenamingNameChange: (value: string) => void;
    onConfirmRename?: () => Promise<void>;
    onCancelRename?: () => void;
    onStartRename?: (id: string, name: string) => void;
    onRestore?: (id: string, name: string) => void;
    onDownload?: (id: string, name: string) => void;
    onVersions?: (id: string, name: string) => void;
    onShare?: (id: string, name: string) => void;
    onCut?: (id: string) => void;
    onDelete?: (id: string, name: string) => void;
  };
  failedPreviews: Set<string>;
  setFailedPreviews: React.Dispatch<React.SetStateAction<Set<string>>>;
}
