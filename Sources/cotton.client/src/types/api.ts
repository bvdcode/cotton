export interface FileManifestDto {
  id: string;
  ownerId?: string | null;
  name: string;
  folder: string;
  contentType: string;
  sizeBytes: number;
  hash: string;
}

export interface CreateFileRequest {
  chunkHashes: string[];
  name: string;
  contentType: string;
  hash: string;
  nodeId: string;
}

export interface LayoutNodeDto {
  id: string;
  userLayoutId: string;
  parentId?: string | null;
  name: string;
  createdAt: string;
  updatedAt: string;
}

export interface LayoutChildrenDto {
  id: string;
  nodes: LayoutNodeDto[];
  files: FileManifestDto[];
}

export interface CreateFolderRequest {
  parentId: string;
  name: string;
}

export interface AuthUser {
  id: string;
  username: string;
  createdAt: string;
  updatedAt: string;
}

export interface CottonResult<T> {
  success: boolean;
  message: string;
  data: T | null;
}

export const NodeType = {
  Default: 0,
  Trash: 1,
} as const;
export type NodeType = (typeof NodeType)[keyof typeof NodeType];
