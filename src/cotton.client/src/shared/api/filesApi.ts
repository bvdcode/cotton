import { httpClient } from "./httpClient";
import type { Guid } from "./layoutsApi";

export interface CreateFileFromChunksRequest {
  nodeId: Guid;
  chunkHashes: string[];
  name: string;
  contentType: string;
  hash: string | null;
  originalNodeFileId?: Guid | null;
}

export interface RenameFileRequest {
  name: string;
}

export const filesApi = {
  createFromChunks: async (request: CreateFileFromChunksRequest): Promise<void> => {
    await httpClient.post("/files/from-chunks", request);
  },

  getDownloadLink: async (nodeFileId: Guid, expireAfterMinutes = 1440): Promise<string> => {
    const response = await httpClient.get<string>(
      `/files/${nodeFileId}/download-link`,
      { params: { expireAfterMinutes } }
    );
    return response.data;
  },

  deleteFile: async (nodeFileId: Guid): Promise<void> => {
    await httpClient.delete(`/files/${nodeFileId}`);
  },

  renameFile: async (nodeFileId: Guid, request: RenameFileRequest): Promise<void> => {
    await httpClient.patch(`/files/${nodeFileId}/rename`, request);
  },
};
