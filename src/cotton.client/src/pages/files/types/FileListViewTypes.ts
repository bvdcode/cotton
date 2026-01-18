import type { NodeDto } from "../../../shared/api/layoutsApi";
import type { NodeFileManifestDto } from "../../../shared/api/nodesApi";

/**
 * Represents a folder tile in the file list view
 */
export interface FolderTile {
  kind: "folder";
  node: NodeDto;
}

/**
 * Represents a file tile in the file list view
 */
export interface FileTile {
  kind: "file";
  file: NodeFileManifestDto;
}

/**
 * Union type for all tile types
 */
export type FileSystemTile = FolderTile | FileTile;

/**
 * Actions that can be performed on a file
 */
export interface FileAction {
  icon: React.ReactNode;
  onClick: () => void;
  tooltip: string;
}

/**
 * Props for folder operation handlers
 */
export interface FolderOperations {
  isRenaming: (folderId: string) => boolean;
  getRenamingName: () => string;
  onRenamingNameChange: (name: string) => void;
  onConfirmRename: () => void;
  onCancelRename: () => void;
  onStartRename: (folderId: string, name: string) => void;
  onDelete: (folderId: string, name: string) => void;
  onClick: (folderId: string) => void;
}

/**
 * Props for file operation handlers
 */
export interface FileOperations {
  isRenaming: (fileId: string) => boolean;
  getRenamingName: () => string;
  onRenamingNameChange: (name: string) => void;
  onConfirmRename: () => Promise<void>;
  onCancelRename: () => void;
  onStartRename: (fileId: string, name: string) => void;
  onDelete: (fileId: string, name: string) => void;
  onDownload: (fileId: string, name: string) => void;
  onClick: (fileId: string, name: string) => void;
  onMediaClick?: (fileId: string) => void;
}

/**
 * Interface for file list view components.
 * Follows the Interface Segregation Principle (ISP) - defines minimal required contract.
 */
export interface IFileListView {
  /**
   * Array of file system items (folders and files) to display
   */
  tiles: FileSystemTile[];

  /**
   * Operations available for folders
   */
  folderOperations: FolderOperations;

  /**
   * Operations available for files
   */
  fileOperations: FileOperations;

  /**
   * Whether a new folder is being created in this view
   */
  isCreatingFolder: boolean;

  /**
   * Name of the new folder being created
   */
  newFolderName: string;

  /**
   * Handler for new folder name change
   */
  onNewFolderNameChange: (name: string) => void;

  /**
   * Handler to confirm creating a new folder
   */
  onConfirmNewFolder: () => Promise<void>;

  /**
   * Handler to cancel creating a new folder
   */
  onCancelNewFolder: () => void;

  /**
   * Placeholder text for folder name input
   */
  folderNamePlaceholder: string;

  /**
   * Placeholder text for file rename input
   */
  fileNamePlaceholder: string;
}
