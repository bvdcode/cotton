export type FileListSourceKind = "nodes" | "trash" | "search" | "share";

export interface FileListCapabilities {
  canUpload: boolean;
  canDelete: boolean;
  canRestore: boolean;
  canRename: boolean;
  canCutPaste: boolean;
  isReadOnly: boolean;
}

export const getFileListCapabilities = (
  sourceKind: FileListSourceKind,
): FileListCapabilities => {
  switch (sourceKind) {
    case "nodes":
      return {
        canUpload: true,
        canDelete: true,
        canRestore: false,
        canRename: true,
        canCutPaste: true,
        isReadOnly: false,
      };
    case "trash":
      return {
        canUpload: false,
        canDelete: true,
        canRestore: true,
        canRename: false,
        canCutPaste: false,
        isReadOnly: false,
      };
    case "search":
      return {
        canUpload: false,
        canDelete: false,
        canRestore: false,
        canRename: false,
        canCutPaste: false,
        isReadOnly: false,
      };
    case "share":
      return {
        canUpload: false,
        canDelete: false,
        canRestore: false,
        canRename: false,
        canCutPaste: false,
        isReadOnly: true,
      };
  }
};
