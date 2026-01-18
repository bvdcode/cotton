import { httpClient } from "./httpClient";
import { InterfaceLayoutType } from "./types/InterfaceLayoutType";

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
  interfaceLayoutType?: InterfaceLayoutType;
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

  /**
   * Updates the UI layout type for a specific node
   * @param nodeId - The ID of the node to update
   * @param layoutType - The new layout type (Tiles or List)
   * @returns The updated node
   */
  updateNodeLayoutType: async (
    nodeId: Guid,
    layoutType: InterfaceLayoutType,
  ): Promise<NodeDto> => {
    const response = await httpClient.patch<NodeDto>(
      `/nodes/${nodeId}/ui-layout-type`,
      null,
      {
        params: { newType: layoutType },
      },
    );
    return response.data;
  },
};
