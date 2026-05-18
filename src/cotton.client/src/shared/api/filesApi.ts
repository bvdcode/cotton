import { httpClient, parseValidated } from "./httpClient";
import type { Guid } from "./layoutsApi";
import type {
  NodeFileManifestDto,
  RestoreOptions,
  RestoreOutcomeDto,
} from "./nodesApi";
import { nodeFileManifestSchema } from "./schemas/node";

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

export type UpdateFileMetadataRequest = Record<string, string>;

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

  updateFileMetadata: async (
    nodeFileId: Guid,
    request: UpdateFileMetadataRequest,
  ): Promise<NodeFileManifestDto> => {
    const url = `/files/${nodeFileId}/metadata`;
    const response = await httpClient.patch<unknown>(url, request);
    return parseValidated(url, response.data, nodeFileManifestSchema);
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
