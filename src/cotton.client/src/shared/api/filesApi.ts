import { httpClient } from "./httpClient";
import type { Guid } from "./layoutsApi";
import type { RestoreOptions, RestoreOutcomeDto } from "./nodesApi";

const downloadLinkInFlight = new Map<string, Promise<string>>();

export interface CreateFileFromChunksRequest {
  nodeId: Guid;
  chunkHashes: string[];
  name: string;
  contentType: string;
  hash: string | null;
  originalNodeFileId?: Guid | null;
  metadata?: Record<string, string>;
}

export interface RenameFileRequest {
  name: string;
}

export interface MoveFileRequest {
  parentId: Guid;
}

export const filesApi = {
  createFromChunks: async (
    request: CreateFileFromChunksRequest,
  ): Promise<void> => {
    await httpClient.post("files/from-chunks", request);
  },

  updateFileContent: async (
    nodeFileId: Guid,
    request: CreateFileFromChunksRequest,
  ): Promise<void> => {
    await httpClient.patch(`files/${nodeFileId}/update-content`, request);
  },

  getDownloadLink: async (
    nodeFileId: Guid,
    expireAfterMinutes = 1440,
  ): Promise<string> => {
    const key = `${nodeFileId}:${expireAfterMinutes}`;
    const existing = downloadLinkInFlight.get(key);
    if (existing) {
      return existing;
    }

    const promise = httpClient
      .get<string>(`files/${nodeFileId}/download-link`, {
        params: { expireAfterMinutes },
      })
      .then((r) => r.data)
      .finally(() => {
        downloadLinkInFlight.delete(key);
      });

    downloadLinkInFlight.set(key, promise);
    return promise;
  },

  deleteFile: async (nodeFileId: Guid, skipTrash = false): Promise<void> => {
    await httpClient.delete(`files/${nodeFileId}`, {
      params: skipTrash ? { skipTrash: true } : undefined,
    });
  },

  renameFile: async (
    nodeFileId: Guid,
    request: RenameFileRequest,
  ): Promise<void> => {
    await httpClient.patch(`/files/${nodeFileId}/rename`, request);
  },

  moveFile: async (
    nodeFileId: Guid,
    request: MoveFileRequest,
  ): Promise<void> => {
    await httpClient.patch(`/files/${nodeFileId}/move`, request);
  },

  restoreFile: async (
    nodeFileId: Guid,
    options: RestoreOptions = {},
  ): Promise<RestoreOutcomeDto> => {
    const response = await httpClient.post<RestoreOutcomeDto>(
      `/files/${nodeFileId}/restore`,
      {
        createMissingParents: options.createMissingParents ?? false,
        overwrite: options.overwrite ?? false,
      },
    );
    return response.data;
  },
};
