import { httpClient } from "./httpClient";

export type Guid = string;

export interface NodeDto {
  id: Guid;
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
    const response = await httpClient.get<NodeDto>(`/layouts/resolver/${normalized}`);
    return response.data;
  },

  getStats: async (layoutId: Guid): Promise<LayoutStatsDto> => {
    const response = await httpClient.get<LayoutStatsDto>(
      `/layouts/${layoutId}/stats`,
    );
    return response.data;
  },
};
