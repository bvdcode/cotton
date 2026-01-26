import { httpClient } from "./httpClient";
import type { BaseDto, Guid, NodeDto } from "./layoutsApi";

export interface NodeFileManifestDto extends BaseDto {
  ownerId: Guid;
  name: string;
  contentType: string;
  sizeBytes: number;
  encryptedFilePreviewHashHex?: string | null;
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
    options?: { nodeType?: string; page?: number; pageSize?: number },
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
        },
      },
    );
    const headersAny = response.headers as unknown as {
      [key: string]: unknown;
      get?: (name: string) => unknown;
    };

    const headerValue =
      headersAny?.["x-total-count"] ??
      headersAny?.["X-Total-Count"] ??
      headersAny?.get?.("x-total-count") ??
      headersAny?.get?.("X-Total-Count");

    const totalCount = Number.parseInt(String(headerValue ?? ""), 10);

    if (!Number.isFinite(totalCount)) {
      throw new Error("x-total-count header is missing or invalid");
    }

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
