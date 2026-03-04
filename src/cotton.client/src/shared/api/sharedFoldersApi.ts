import { httpClient } from "./httpClient";
import type { Guid, NodeDto } from "./layoutsApi";
import type { NodeContentDto } from "./nodesApi";

export interface SharedFolderInfoDto {
  name: string;
  nodeId: Guid;
  createdAt: string;
  expiresAt: string | null;
}

export interface SharedFolderChildrenResponse {
  content: NodeContentDto;
  totalCount: number;
}

export const sharedFoldersApi = {
  getInfo: async (token: string): Promise<SharedFolderInfoDto> => {
    const response = await httpClient.get<SharedFolderInfoDto>(
      `/shared/folders/${encodeURIComponent(token)}`,
    );
    return response.data;
  },

  getChildren: async (
    token: string,
    options?: { page?: number; pageSize?: number },
  ): Promise<SharedFolderChildrenResponse> => {
    const page = options?.page ?? 1;
    const pageSize = options?.pageSize ?? 1000000;
    const response = await httpClient.get<NodeContentDto>(
      `/shared/folders/${encodeURIComponent(token)}/children`,
      { params: { page, pageSize } },
    );
    const totalCount = parseTotalCount(response.headers);
    return { content: response.data, totalCount };
  },

  getSubfolderChildren: async (
    token: string,
    nodeId: Guid,
    options?: { page?: number; pageSize?: number },
  ): Promise<SharedFolderChildrenResponse> => {
    const page = options?.page ?? 1;
    const pageSize = options?.pageSize ?? 1000000;
    const response = await httpClient.get<NodeContentDto>(
      `/shared/folders/${encodeURIComponent(token)}/nodes/${nodeId}/children`,
      { params: { page, pageSize } },
    );
    const totalCount = parseTotalCount(response.headers);
    return { content: response.data, totalCount };
  },

  getAncestors: async (token: string, nodeId: Guid): Promise<NodeDto[]> => {
    const response = await httpClient.get<NodeDto[]>(
      `/shared/folders/${encodeURIComponent(token)}/nodes/${nodeId}/ancestors`,
    );
    return response.data;
  },

  buildFileDownloadUrl: (token: string, nodeFileId: Guid): string =>
    `/api/v1/shared/folders/${encodeURIComponent(token)}/files/${nodeFileId}/download`,

  buildFileInlineUrl: (token: string, nodeFileId: Guid): string =>
    `/api/v1/shared/folders/${encodeURIComponent(token)}/files/${nodeFileId}/download?inline=true`,
};

function parseTotalCount(headers: Record<string, unknown>): number {
  const headersAny = headers as unknown as {
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

  return totalCount;
}
