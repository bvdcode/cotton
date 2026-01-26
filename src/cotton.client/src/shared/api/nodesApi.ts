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
    const headerValue = response.headers["x-total-count"];
    const parsed = Number.parseInt(String(headerValue ?? ""), 10);

    const pageItemCount =
      (response.data.nodes?.length ?? 0) + (response.data.files?.length ?? 0);

    // If backend provides the total count header, trust it.
    if (Number.isFinite(parsed)) {
      return { content: response.data, totalCount: parsed };
    }

    // Backend didn't provide total count. For large (effectively unpaged) requests,
    // fall back to the returned item count.
    if (requestedPageSize >= 1000000) {
      return { content: response.data, totalCount: pageItemCount };
    }

    // For paged requests without a total header, return an estimate that still
    // enables DataGrid paging.
    // - If the page is full, assume there may be at least one more item.
    // - If not full, we reached the end: compute exact total up to this page.
    const estimatedTotalCount =
      pageItemCount >= requestedPageSize
        ? requestedPage * requestedPageSize + 1
        : (requestedPage - 1) * requestedPageSize + pageItemCount;

    return { content: response.data, totalCount: estimatedTotalCount };
  },

  createNode: async (request: CreateNodeRequest): Promise<NodeDto> => {
    const response = await httpClient.put<NodeDto>("/layouts/nodes", request);
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
