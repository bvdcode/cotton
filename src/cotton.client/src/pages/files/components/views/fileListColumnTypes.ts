import type React from "react";
import type { FolderEncryptionPolicyState } from "@shared/crypto";

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
  tile?: {
    kind: "folder" | "file";
    file?: {
      id: string;
      name: string;
      previewHashEncryptedHex?: string | null;
    };
  };
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
