import type { NodeContentDto, NodeFileManifestDto } from "../api/nodesApi";
import type { NodeDto } from "../api/layoutsApi";

export type NodesState = {
  cacheOwnerUserId: string | null;
  currentNodeId: string | null;
  rootNodeId: string | null;
  navigationVersion: number;
  loading: boolean;
  error: string | null;
  updateNode: (updated: NodeDto) => void;
  moveFolderInCache: (
    updated: NodeDto,
    sourceParentId: string,
    targetParentId: string,
  ) => void;
  moveFileInCache: (
    updated: NodeFileManifestDto,
    sourceParentId: string,
    targetParentId: string,
  ) => void;
  addFolderToCache: (parentNodeId: string, folder: NodeDto) => void;
  updateFileInCache: (
    parentNodeId: string,
    file: NodeContentDto["files"][number],
  ) => void;
  upsertFileInCache: (
    parentNodeId: string,
    file: NodeContentDto["files"][number],
  ) => boolean;
  optimisticRenameFile: (
    parentNodeId: string,
    fileId: string,
    newName: string,
  ) => void;
  optimisticSetFilePreviewHash: (
    parentNodeId: string,
    fileId: string,
    previewHashEncryptedHex: string,
  ) => boolean;
  optimisticDeleteFile: (parentNodeId: string, fileId: string) => void;
  refreshCachedFileDisplayMetadata: () => Promise<void>;
  reset: (cacheOwnerUserId?: string | null) => void;
};
