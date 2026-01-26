import { httpClient } from "./httpClient";
import { InterfaceLayoutType } from "./types/InterfaceLayoutType";
import type { NodeFileManifestDto } from "./nodesApi";

export { InterfaceLayoutType };

export type Guid = string;

export interface BaseDto {
  id: Guid;
  createdAt: string;
  updatedAt: string;
}

export interface NodeDto extends BaseDto {
  layoutId: Guid;
  parentId: Guid | null;
  name: string;
}

export interface LayoutStatsDto {
  layoutId: Guid;
  nodeCount: number;
  fileCount: number;
  sizeBytes: number;
}

export interface LayoutSearchResultDto {
  nodes: NodeDto[];
  files: NodeFileManifestDto[];
}

export interface LayoutSearchResult {
  data: LayoutSearchResultDto;
  totalCount: number;
}

const joinResolverPath = (path: string): string => {
  // Backend has two routes:
  //  - GET /layouts/resolver
  //  - GET /layouts/resolver/{*path}
  // We want to keep slashes as path separators but still URL-encode each segment.
  const parts = path.split("/").filter((p) => p.length > 0);
  const encoded = parts.map((p) => encodeURIComponent(p));
  return encoded.join("/");
};

export const layoutsApi = {
  resolve: async (options?: {
    path?: string | null;
    nodeType?: string;
  }): Promise<NodeDto> => {
    const path = options?.path ?? null;
    const nodeType = options?.nodeType;

    if (path == null || path.trim().length === 0) {
      const response = await httpClient.get<NodeDto>("/layouts/resolver", {
        params: nodeType ? { nodeType } : undefined,
      });
      return response.data;
    }

    const normalized = joinResolverPath(path);
    const response = await httpClient.get<NodeDto>(
      `/layouts/resolver/${normalized}`,
    );
    return response.data;
  },

  getStats: async (layoutId: Guid): Promise<LayoutStatsDto> => {
    const response = await httpClient.get<LayoutStatsDto>(
      `/layouts/${layoutId}/stats`,
    );
    return response.data;
  },

  search: async (options: {
    layoutId: Guid;
    query: string;
    page?: number;
    pageSize?: number;
  }): Promise<LayoutSearchResult> => {
    const { layoutId, query, page = 1, pageSize = 20 } = options;

    const response = await httpClient.get<LayoutSearchResultDto>(
      `/layouts/${layoutId}/search`,
      {
        params: { query, page, pageSize },
      },
    );

    const headerRaw = response.headers["x-total-count"];
    const totalCount = headerRaw ? parseInt(headerRaw, 10) : 0;

    return {
      data: response.data,
      totalCount,
    };
  },
};
