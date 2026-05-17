import { getValidated, httpClient, parseValidated } from "./httpClient";
import type { BaseDto, Guid, NodeDto } from "./layoutsApi";
import { readRequiredIntHeader, type HeaderMap } from "./utils/headerUtils";
import {
  nodeContentSchema,
  nodeDtoSchema,
  restoreOutcomeSchema,
} from "./schemas/node";
import { z } from "zod";

export interface NodeFileManifestDto extends BaseDto {
  /**
   * Container node id (folder) where this file is located.
   */
  nodeId: Guid;
  ownerId: Guid;
  name: string;
  contentType: string;
  sizeBytes: number;
  metadata: Record<string, string>;
  requiresVideoTranscoding?: boolean;
  previewHashEncryptedHex?: string | null;
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

export interface MoveNodeRequest {
  parentId: Guid;
}

export type RestoreStatus =
  | "Restored"
  | "ParentMissing"
  | "Conflict"
  | "NotRestorable";

export type RestoreConflictKind = "Folder" | "File";

export interface RestoreOutcomeDto {
  status: RestoreStatus;
  originalParentPath?: string | null;
  missingPath?: string | null;
  conflictKind?: RestoreConflictKind | null;
  conflictName?: string | null;
  restoredNode?: NodeDto | null;
  restoredFile?: NodeFileManifestDto | null;
  reason?: string | null;
}

export interface RestoreOptions {
  createMissingParents?: boolean;
  overwrite?: boolean;
}

const nodeListSchema = z.array(nodeDtoSchema);

export const nodesApi = {
  getNode: (nodeId: Guid): Promise<NodeDto> =>
    getValidated(`/layouts/nodes/${nodeId}`, nodeDtoSchema),

  getAncestors: async (
    nodeId: Guid,
    options?: { nodeType?: string },
  ): Promise<NodeDto[]> =>
    getValidated(
      `/layouts/nodes/${nodeId}/ancestors`,
      nodeListSchema,
      {
        params: options?.nodeType ? { nodeType: options.nodeType } : undefined,
      },
    ),

  getChildren: async (
    nodeId: Guid,
    options?: { nodeType?: string; page?: number; pageSize?: number; depth?: number },
  ): Promise<NodeResponse> => {
    const requestedPage = options?.page ?? 1;
    const requestedPageSize = options?.pageSize ?? 1000000;
    const url = `/layouts/nodes/${nodeId}/children`;
    const response = await httpClient.get<unknown>(url, {
      params: {
        page: requestedPage,
        pageSize: requestedPageSize,
        nodeType: options?.nodeType,
        depth: options?.depth,
      },
    });
    const content = parseValidated(url, response.data, nodeContentSchema);
    const totalCount = readRequiredIntHeader(response.headers as HeaderMap, "x-total-count");

    return { content, totalCount };
  },

  createNode: async (request: CreateNodeRequest): Promise<NodeDto> => {
    const url = "layouts/nodes";
    const response = await httpClient.put<unknown>(url, request);
    return parseValidated(url, response.data, nodeDtoSchema);
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
    const url = `/layouts/nodes/${nodeId}/rename`;
    const response = await httpClient.patch<unknown>(url, request);
    return parseValidated(url, response.data, nodeDtoSchema);
  },

  moveNode: async (
    nodeId: Guid,
    request: MoveNodeRequest,
  ): Promise<NodeDto> => {
    const url = `/layouts/nodes/${nodeId}/move`;
    const response = await httpClient.patch<unknown>(url, request);
    return parseValidated(url, response.data, nodeDtoSchema);
  },

  updateNodeMetadata: async (
    nodeId: Guid,
    patch: Record<string, string>,
  ): Promise<NodeDto> => {
    const url = `/layouts/nodes/${nodeId}/metadata`;
    const response = await httpClient.patch<unknown>(url, patch);
    return parseValidated(url, response.data, nodeDtoSchema);
  },

  restoreNode: async (
    nodeId: Guid,
    options: RestoreOptions = {},
  ): Promise<RestoreOutcomeDto> => {
    const url = `/layouts/nodes/${nodeId}/restore`;
    const response = await httpClient.post<unknown>(url, {
      createMissingParents: options.createMissingParents ?? false,
      overwrite: options.overwrite ?? false,
    });
    return parseValidated(url, response.data, restoreOutcomeSchema);
  },
};
