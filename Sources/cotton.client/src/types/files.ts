export interface FileItem {
  id: string;
  name: string;
  contentType: string;
  sizeBytes?: number;
  createdAt?: string;
  updatedAt?: string;
}

export interface NodeItem {
  id: string;
  name: string;
  parentId?: string | null;
  createdAt?: string;
  updatedAt?: string;
}

export interface ChildrenResponse {
  nodes: NodeItem[];
  files: FileItem[];
}

export interface CreateFolderRequest {
  parentId: string;
  name: string;
}

export interface CreateFileFromChunksRequest {
  chunkHashes: string[];
  name: string;
  contentType: string;
  hash: string;
  nodeId: string;
}

export interface ChunkExistsResponse {
  exists: boolean;
}
