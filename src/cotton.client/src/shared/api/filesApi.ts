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

export const filesApi = {
  createFromChunks: async (request: CreateFileFromChunksRequest): Promise<void> => {
    await httpClient.post("/files/from-chunks", request);
  },

  getDownloadLink: async (nodeFileId: Guid): Promise<string> => {
    const response = await httpClient.get<string>(`/files/${nodeFileId}/download`);
    return response.data;
  },
};
