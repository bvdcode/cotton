import { httpClient } from "./httpClient";
import type { BaseDto, Guid, NodeDto } from "./layoutsApi";
import { readRequiredIntHeader, type HeaderMap } from "./utils/headerUtils";

export interface NodeFileManifestDto extends BaseDto {
  /**
   * Container node id (folder) where this file is located.
   * Present in search results; may be omitted in other endpoints.
   */
  nodeId?: Guid;
  ownerId?: Guid;
  name: string;
  contentType: string;
  sizeBytes: number;
  previewHashEncryptedHex?: string | null;
  largeFilePreviewPresignedToken?: string | null;
}
export interface NodeResponse {
  content: NodeContentDto;
  totalCount: number;
}

export interface NodeContentDto extends BaseDto {
  nodes: NodeDto[];
  files: NodeFileManifestDto[];
}

export interface CreateNodeRequest {
  parentId: Guid;
  name: string;
}

export interface RenameNodeRequest {
  name: string;
}

export const nodesApi = {
  getNode: async (nodeId: Guid): Promise<NodeDto> => {
    const response = await httpClient.get<NodeDto>(`/layouts/nodes/${nodeId}`);
    return response.data;
  },

  getAncestors: async (
    nodeId: Guid,
    options?: { nodeType?: string },
  ): Promise<NodeDto[]> => {
    const response = await httpClient.get<NodeDto[]>(
      `/layouts/nodes/${nodeId}/ancestors`,
      {
        params: options?.nodeType ? { nodeType: options.nodeType } : undefined,
      },
    );
    return response.data;
  },

  getChildren: async (
    nodeId: Guid,
    options?: { nodeType?: string; page?: number; pageSize?: number; depth?: number },
  ): Promise<NodeResponse> => {
    const requestedPage = options?.page ?? 1;
    const requestedPageSize = options?.pageSize ?? 1000000;
    const response = await httpClient.get<NodeContentDto>(
      `/layouts/nodes/${nodeId}/children`,
      {
        params: {
          page: requestedPage,
          pageSize: requestedPageSize,
          nodeType: options?.nodeType,
          depth: options?.depth,
        },
      },
    );
    const totalCount = readRequiredIntHeader(response.headers as HeaderMap, "x-total-count");

    return { content: response.data, totalCount };
  },

  createNode: async (request: CreateNodeRequest): Promise<NodeDto> => {
    const response = await httpClient.put<NodeDto>("layouts/nodes", request);
    return response.data;
  },

  deleteNode: async (nodeId: Guid, skipTrash = false): Promise<void> => {
    await httpClient.delete(`/layouts/nodes/${nodeId}`, {
      params: skipTrash ? { skipTrash: true } : undefined,
    });
  },

  renameNode: async (
    nodeId: Guid,
    request: RenameNodeRequest,
  ): Promise<NodeDto> => {
    const response = await httpClient.patch<NodeDto>(
      `/layouts/nodes/${nodeId}/rename`,
      request,
    );
    return response.data;
  },
};
