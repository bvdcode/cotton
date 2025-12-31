import { httpClient } from "./httpClient";
import type { Guid, NodeDto } from "./layoutsApi";

export interface NodeFileManifestDto {
  id: Guid;
  ownerId: Guid;
  name: string;
  contentType: string;
  sizeBytes: number;
}

export interface NodeContentDto {
  id: Guid;
  nodes: NodeDto[];
  files: NodeFileManifestDto[];
}

export const nodesApi = {
  getNode: async (nodeId: Guid): Promise<NodeDto> => {
    const response = await httpClient.get<NodeDto>(`/layouts/nodes/${nodeId}`);
    return response.data;
  },

  getAncestors: async (nodeId: Guid, options?: { nodeType?: string }): Promise<NodeDto[]> => {
    const response = await httpClient.get<NodeDto[]>(`/layouts/nodes/${nodeId}/ancestors`, {
      params: options?.nodeType ? { nodeType: options.nodeType } : undefined,
    });
    return response.data;
  },

  getChildren: async (nodeId: Guid, options?: { nodeType?: string }): Promise<NodeContentDto> => {
    const response = await httpClient.get<NodeContentDto>(`/layouts/nodes/${nodeId}/children`, {
      params: options?.nodeType ? { nodeType: options.nodeType } : undefined,
    });
    return response.data;
  },
};
